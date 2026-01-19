using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers;
using Frame.Core;
using Frame.ECS;
using UnityEngine;
using Google.Protobuf;
using Proto;
using System.Net.Sockets.Kcp;

/// <summary>
/// 帧同步网络管理器（KCP版本）
/// 使用KCP协议替代TCP，提供更低的延迟
/// 
/// 依赖：System.Net.Sockets.Kcp（已安装）
/// 使用SimpleSegManager.Kcp实现KCP协议
/// </summary>
public class FrameSyncNetworkKCP : SingletonMono<FrameSyncNetworkKCP>,INetwork
{
    [Header("服务器设置")] public string serverIP = "127.0.0.1";
    public int serverPort = 8088; // KCP端口
    public string playerName = "Player";

    [Header("KCP配置")] [Tooltip("发送窗口大小（必须与服务器一致）")] public uint sendWindowSize = 128;
    [Tooltip("接收窗口大小（必须与服务器一致）")] public uint receiveWindowSize = 128;
    [Tooltip("最大传输单元")] public uint mtu = 1400;
    [Tooltip("快速重传阈值")] public uint fastResend = 2;
    [Tooltip("无延迟模式")] public bool noDelay = true;
    [Tooltip("内部更新间隔（毫秒）")] public uint interval = 10;
    [Tooltip("快速重传触发阈值")] public uint resend = 2;
    [Tooltip("最小RTO（毫秒）")] public uint minRto = 30;

    [Header("状态")] public bool isConnected = false;
    public bool isGameStarted = false;
    public int myPlayerID;

    // KCP客户端
    private UdpClient udpClient;
    private SimpleSegManager.Kcp kcp;
    private IPEndPoint serverEndPoint;
    private const uint KCP_CONV = 2001; // KCP会话ID，客户端和服务器必须一致

    private Thread receiveThread;
    private Thread updateThread;
    private Thread heartbeatThread;
    private Thread connectThread;
    private bool isRunning = false;
    private bool isConnecting = false;
    private readonly object threadLock = new object();
    private CancellationTokenSource cancellationTokenSource;

    // 消息队列（线程安全）
    private Queue<(MessageType, IMessage)> serverDataQueue = new Queue<(MessageType, IMessage)>();
    private object queueLock = new object();

    bool INetwork.IsConnected => isConnected;
    bool INetwork.IsGameStarted => isGameStarted;
    int INetwork.MyID => myPlayerID;
    
    // 事件回调
    public event System.Action<ServerFrame> OnServerFrameReceived;
    public event System.Action<long> OnConnected;
    public event System.Action<GameStart> OnGameStarted;
    public event System.Action OnDisconnected;

    void Start()
    {
        // 可以在这里自动连接，或者通过外部调用 Connect()
    }

    void OnDestroy()
    {
        Disconnect();
    }

    void OnApplicationQuit()
    {
        Disconnect();
    }

    void Update()
    {
        // 在主线程中处理消息队列
        ProcessMessageQueue();
        // 注意：KCP更新在KCPUpdateLoop线程中进行，这里不需要重复更新
    }

    /// <summary>
    /// 连接到服务器（KCP）
    /// </summary>
    public void Connect()
    {
        if (Instance != this)
        {
            Debug.LogWarning($"Connect() called on non-singleton instance of {GetType().Name}. Ignoring.");
            return;
        }

        if (isConnected)
        {
            Debug.LogWarning("Already connected to server");
            return;
        }

        lock (threadLock)
        {
            if (isConnected || isConnecting)
            {
                Debug.LogWarning(
                    $"Connection already in progress (isConnected: {isConnected}, isConnecting: {isConnecting})");
                return;
            }

            isConnecting = true;
        }

        // 创建取消令牌
        cancellationTokenSource = new CancellationTokenSource();
        
        // 在后台线程中执行连接
        connectThread = new Thread(() =>
        {
            try
            {
                Debug.Log($"Attempting to connect to KCP server {serverIP}:{serverPort}...");

                // 创建UDP客户端（绑定任意可用端口）
                udpClient = new UdpClient(0); // 0表示系统自动分配端口
                serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

                // 创建KCP实例
                kcp = new SimpleSegManager.Kcp(KCP_CONV, new KcpCallbackImpl(this));

                // 配置KCP参数（快速模式，低延迟）
                if (noDelay)
                {
                    kcp.NoDelay(1, (int)interval, (int)resend, 1); // nodelay, interval, resend, nc
                }
                else
                {
                    kcp.NoDelay(0, (int)interval, 0, 0);
                }

                kcp.WndSize((int)sendWindowSize, (int)receiveWindowSize);
                kcp.SetMtu((int)mtu);

                // 启动接收线程
                isRunning = true;
                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "KCP Receive Thread"
                };
                receiveThread.Start();

                // 启动KCP更新线程（定期调用kcp.Update()来发送握手包和保持连接）
                updateThread = new Thread(KCPUpdateLoop)
                {
                    IsBackground = true,
                    Name = "KCP Update Thread"
                };
                updateThread.Start();

                // 等待一小段时间，确保KCP更新线程已启动
                Thread.Sleep(50);

                // 发送一个初始握手消息来触发KCP连接建立
                // KCP需要先发送数据包，服务器端的AcceptKCP()才会返回
                // 这里发送一个空的ConnectMessage来触发握手（服务器端会忽略这个消息，但会建立KCP连接）
                try
                {
                    var connectMsg = new ConnectMessage
                    {
                        PlayerName = playerName
                    };
                    byte[] data = connectMsg.ToByteArray();
                    byte[] lengthBytes = BitConverter.GetBytes((uint)(1 + data.Length));
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(lengthBytes);
                    byte[] messageTypeBytes = new byte[] { (byte)MessageType.MessageConnect };
                    byte[] message = new byte[4 + 1 + data.Length];
                    Buffer.BlockCopy(lengthBytes, 0, message, 0, 4);
                    Buffer.BlockCopy(messageTypeBytes, 0, message, 4, 1);
                    Buffer.BlockCopy(data, 0, message, 5, data.Length);

                    kcp.Send(message);
                    kcp.Update(DateTimeOffset.UtcNow);
                    Debug.Log("Sent initial KCP handshake message");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to send initial handshake: {e.Message}");
                }

                lock (threadLock)
                {
                    // 注意：此时isConnected设为true只是表示UDP和KCP已初始化
                    // 真正的连接成功需要等待服务器返回ConnectMessage
                    isConnecting = false;
                }

                isGameStarted = false;

                Debug.Log($"KCP client initialized, waiting for server response...");
            }
            catch (Exception e)
            {
                Debug.LogError($"KCP Connection error: {e.Message}\n{e.StackTrace}");
                lock (threadLock)
                {
                    isConnected = false;
                    isConnecting = false;
                }

                // 清理资源
                if (udpClient != null)
                {
                    try
                    {
                        udpClient.Close();
                    }
                    catch
                    {
                    }

                    udpClient = null;
                }

                kcp = null;
            }
        });
        connectThread.IsBackground = true;
        connectThread.Start();
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public void Disconnect()
    {
        lock (threadLock)
        {
            if (!isConnected && !isConnecting && !isRunning)
                return;

            isRunning = false;
            isConnecting = false;
            isConnected = false;
        }

        // 取消所有操作
        if (cancellationTokenSource != null)
        {
            try
            {
                cancellationTokenSource.Cancel();
            }
            catch
            {
            }
        }

        // 关闭UDP连接（这会中断阻塞的Receive调用）
        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
                udpClient.Dispose();
            }
            catch
            {
            }

            udpClient = null;
        }

        // 清理KCP
        if (kcp != null)
        {
            try
            {
                kcp.Dispose();
            }
            catch
            {
            }

            kcp = null;
        }

        // 等待并清理所有线程
        CleanupThread(ref receiveThread, "Receive");
        CleanupThread(ref updateThread, "Update");
        CleanupThread(ref heartbeatThread, "Heartbeat");
        CleanupThread(ref connectThread, "Connect");

        // 清理取消令牌
        if (cancellationTokenSource != null)
        {
            try
            {
                cancellationTokenSource.Dispose();
            }
            catch
            {
            }
            cancellationTokenSource = null;
        }

        OnDisconnected?.Invoke();
        Debug.Log("Disconnected from KCP server");
    }

    /// <summary>
    /// 清理线程（安全地等待和终止）
    /// </summary>
    private void CleanupThread(ref Thread thread, string threadName)
    {
        if (thread != null)
        {
            try
            {
                if (thread.IsAlive)
                {
                    // 等待线程自然退出（最多等待2秒）
                    if (!thread.Join(2000))
                    {
                        Debug.LogWarning($"{threadName} thread did not exit in time, it may still be running");
                        // 注意：在Unity中，不建议使用Abort()，因为可能导致资源泄漏
                        // 线程应该通过检查isRunning标志来自然退出
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error cleaning up {threadName} thread: {e.Message}");
            }
            finally
            {
                thread = null;
            }
        }
    }

    /// <summary>
    /// 发送帧数据
    /// </summary>
    public void SendFrameData(InputDirection direction, bool isFire = false, long fireX = 0, long fireY = 0,bool isToggle = false)
    {
        if (!isConnected)
        {
            Debug.LogWarning("Not connected to server");
            return;
        }
        

        var frameData = new FrameData
        {
            PlayerId = myPlayerID,
            Direction = direction,
            FrameNumber = ECSPredictionRollbackManager.Instance.confirmedServerFrame,
            IsFire = isFire,
            IsToggle = isToggle
        };

        // 如果发射，设置目标位置
        if (isFire)
        {
            frameData.FireX = fireX;
            frameData.FireY = fireY;
        }

        Send(MessageType.MessageFrameData, frameData);
    }

    /// <summary>
    /// 发送丢帧请求
    /// </summary>
    public void SendLossFrame(long frameNumber)
    {
        if (!isConnected)
        {
            Debug.LogWarning("Not connected to server");
            return;
        }

        try
        {
            var frameLoss = new GetLossFrame() { LastFrameNumber = frameNumber };
            byte[] data = frameLoss.ToByteArray();

            byte[] lengthBytes = BitConverter.GetBytes((uint)(1 + data.Length));
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);

            byte[] messageTypeBytes = new byte[] { (byte)MessageType.MessageFrameLoss };

            byte[] message = new byte[4 + 1 + data.Length];
            Buffer.BlockCopy(lengthBytes, 0, message, 0, 4);
            Buffer.BlockCopy(messageTypeBytes, 0, message, 4, 1);
            Buffer.BlockCopy(data, 0, message, 5, data.Length);

            // 通过KCP发送
            if (kcp != null && serverEndPoint != null)
            {
                kcp.Send(message);
                kcp.Update(DateTimeOffset.UtcNow);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"SendLossFrame error: {e.Message}");
        }
    }

    public void SendHeartbeat()
    {
        if (isConnected)
        {
            Send(MessageType.MessageHeartbeat, new Heartbeat());
        }
    }

    /// <summary>
    /// 启动心跳线程（每5秒发送一次心跳）
    /// </summary>
    private void StartHeartbeatThread()
    {
        // 如果已有心跳线程在运行，先停止它
        if (heartbeatThread != null && heartbeatThread.IsAlive)
        {
            return; // 心跳线程已经在运行
        }

        heartbeatThread = new Thread(() =>
        {
            try
            {
                const int heartbeatInterval = 5000; // 5秒发送一次心跳
                
                while (isRunning && isConnected)
                {
                    try
                    {
                        SendHeartbeat();
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Error sending heartbeat: {e.Message}");
                    }

                    // 等待心跳间隔，但每100ms检查一次isRunning，以便快速响应断开
                    for (int i = 0; i < heartbeatInterval / 100 && isRunning && isConnected; i++)
                    {
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception e)
            {
                if (isRunning)
                {
                    Debug.LogError($"Heartbeat thread error: {e.Message}");
                }
            }
            finally
            {
                Debug.Log("Heartbeat thread exited");
            }
        })
        {
            IsBackground = true,
            Name = "KCP Heartbeat Thread"
        };
        heartbeatThread.Start();
    }

    public void Send(MessageType messageType, IMessage imessage)
    {
        try
        {
            // 序列化消息
            byte[] data = imessage.ToByteArray();

            // 消息格式：len(4 bytes) + messageType(1 byte) + data
            byte[] lengthBytes = BitConverter.GetBytes((uint)(1 + data.Length));
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);

            byte[] messageTypeBytes = new byte[] { (byte)messageType };

            // 组合消息
            byte[] message = new byte[4 + 1 + data.Length];
            Buffer.BlockCopy(lengthBytes, 0, message, 0, 4);
            Buffer.BlockCopy(messageTypeBytes, 0, message, 4, 1);
            Buffer.BlockCopy(data, 0, message, 5, data.Length);

            // 通过KCP发送
            if (kcp != null && serverEndPoint != null)
            {
                kcp.Send(message);
                kcp.Update(DateTimeOffset.UtcNow);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"SendFrameData error: {e.Message}");
        }
    }


    /// <summary>
    /// 处理KCP接收到的数据（可能包含多个消息）
    /// </summary>
    private void ProcessKCPData(byte[] data)
    {
        int offset = 0;
        Debug.Log($"[KCP] Received data: {data.Length} bytes, offset: {offset}");

        while (offset < data.Length)
        {
            // 检查是否有足够的数据读取消息长度（至少需要4字节）
            if (offset + 4 > data.Length)
            {
                Debug.LogWarning(
                    $"Incomplete message: need 4 bytes for length, but only {data.Length - offset} bytes available");
                break;
            }

            // 读取消息长度（4字节，大端序）
            byte[] lengthBytes = new byte[4];
            Array.Copy(data, offset, lengthBytes, 0, 4);

            // 先保存原始字节用于调试
            byte[] originalLengthBytes = new byte[4];
            Array.Copy(lengthBytes, originalLengthBytes, 4);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);
            uint totalLength = BitConverter.ToUInt32(lengthBytes, 0);


            // 检查长度是否合理（至少包含消息类型1字节）
            if (totalLength < 1)
            {
                Debug.LogError($"[KCP] Invalid message length: {totalLength} (must be >= 1)");
                break;
            }

            if (totalLength > 1024 * 1024) // 最大1MB
            {
                Debug.LogError($"[KCP] Message too large: {totalLength} bytes (max 1MB)");
                break;
            }

            // 检查是否有足够的数据读取完整消息
            int requiredBytes = 4 + (int)totalLength;
            int availableBytes = data.Length - offset;
            if (availableBytes < requiredBytes)
            {
                Debug.LogWarning(
                    $"[KCP] Incomplete message: need {requiredBytes} bytes, but only {availableBytes} bytes available. Waiting for more data...");
                break; // 等待更多数据
            }

            // 读取消息类型（1字节）
            MessageType messageType = (MessageType)data[offset + 4];
            Debug.Log($"[KCP] Message type: {messageType} (value: {(int)messageType}), data length: {totalLength - 1}");

            // 读取数据部分
            int dataLength = (int)totalLength - 1;
            byte[] messageData = new byte[dataLength];
            if (dataLength > 0)
            {
                Array.Copy(data, offset + 5, messageData, 0, dataLength);
            }

            // 解析消息
            try
            {
                IMessage message = null;
                switch (messageType)
                {
                    case MessageType.MessageConnect:
                        message = ConnectMessage.Parser.ParseFrom(messageData);
                        Debug.Log(
                            $"[KCP] Parsed ConnectMessage: PlayerId={((ConnectMessage)message).PlayerId}, PlayerName={((ConnectMessage)message).PlayerName}");
                        break;
                    case MessageType.MessageServerFrame:
                        message = ServerFrame.Parser.ParseFrom(messageData);
                        Debug.Log($"[KCP] Parsed ServerFrame");
                        break;
                    case MessageType.MessageGameStart:
                        message = GameStart.Parser.ParseFrom(messageData);
                        Debug.Log($"[KCP] Parsed GameStart");
                        break;
                    default:
                        Debug.LogWarning($"[KCP] Unknown message type: {messageType} (value: {(int)messageType})");
                        // 跳过这个消息，继续处理下一个
                        offset += 4 + (int)totalLength;
                        continue;
                }

                // 加入消息队列（在主线程处理）
                if (message != null)
                {
                    lock (queueLock)
                    {
                        serverDataQueue.Enqueue((messageType, message));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[KCP] Failed to parse message type {messageType}: {e.Message}\n{e.StackTrace}");
                // 即使解析失败，也要移动到下一个消息，避免无限循环
                offset += 4 + (int)totalLength;
                continue;
            }

            // 移动到下一个消息
            offset += 4 + (int)totalLength;
        }
    }

    /// <summary>
    /// 处理消息队列（在主线程中调用）
    /// </summary>
    private void ProcessMessageQueue()
    {
        lock (queueLock)
        {
            while (serverDataQueue.Count > 0)
            {
                var (messageType, message) = serverDataQueue.Dequeue();
                HandleMessage(messageType, message);
            }
        }
    }

    /// <summary>
    /// 处理消息
    /// </summary>
    private void HandleMessage(MessageType messageType, IMessage message)
    {
        switch (messageType)
        {
            case MessageType.MessageConnect:
                if (message is ConnectMessage connectMsg)
                {
                    myPlayerID = connectMsg.PlayerId;
                    lock (threadLock)
                    {
                        isConnected = true; // 收到服务器响应后，才真正标记为已连接
                    }

                    OnConnected?.Invoke(connectMsg.PlayerId);
                    Debug.Log($"Connected to KCP server, PlayerID: {myPlayerID}");

                    // 启动心跳线程（定期发送心跳）
                    StartHeartbeatThread();
                }

                break;

            case MessageType.MessageServerFrame:
                if (message is ServerFrame serverFrame)
                {
                    OnServerFrameReceived?.Invoke(serverFrame);
                }

                break;

            case MessageType.MessageGameStart:
                if (message is GameStart gameStart)
                {
                    isGameStarted = true;
                    OnGameStarted?.Invoke(gameStart);
                    Debug.Log("Game started!");
                }

                break;

            case MessageType.MessageDisconnect:
                // 处理断开连接消息（可能来自服务器或本地检测到的连接关闭）
                Debug.Log("Received disconnect message or connection closed");
                lock (threadLock)
                {
                    if (isConnected) // 只在已连接状态下处理，避免重复触发
                    {
                        isConnected = false;
                        isGameStarted = false;
                        // 触发断开连接回调
                        OnDisconnected?.Invoke();
                    }
                }
                // 注意：不在这里调用 Disconnect()，因为可能已经在清理过程中
                // 如果需要完全清理，应该由外部调用 Disconnect()
                break;

            default:
                Debug.LogWarning($"Unhandled message type: {messageType}");
                break;
        }
    }

    /// <summary>
    /// UDP接收循环（在后台线程中运行）
    /// </summary>
    private void ReceiveLoop()
    {
        try
        {
            while (isRunning && udpClient != null)
            {
                try
                {
                    // 接收UDP数据（使用异步方式避免长时间阻塞）
                    IPEndPoint remoteEndPoint = null;
                    byte[] data = null;
                    
                    try
                    {
                        // Receive是阻塞的，但如果UDP客户端关闭，会抛出异常
                        data = udpClient.Receive(ref remoteEndPoint);
                    }
                    catch (ObjectDisposedException)
                    {
                        // UDP客户端已关闭，正常退出
                        break;
                    }
                    catch (SocketException e)
                    {
                        // Socket错误，检查是否是关闭导致的
                        if (!isRunning)
                        {
                            break; // 正常关闭
                        }
                        
                        // 检查Socket错误代码，区分不同类型的错误
                        SocketError errorCode = e.SocketErrorCode;
                        
                        // 连接被远程主机关闭的错误（不可恢复）
                        if (errorCode == SocketError.ConnectionReset || 
                            errorCode == SocketError.ConnectionAborted ||
                            errorCode == SocketError.Shutdown ||
                            errorCode == SocketError.NetworkReset)
                        {
                            Debug.LogWarning($"Connection closed by remote host: {e.Message} (ErrorCode: {errorCode})");
                            // 触发断开连接回调（在主线程中执行）
                            lock (threadLock)
                            {
                                isConnected = false;
                            }
                            // 使用队列通知主线程触发断开回调
                            lock (queueLock)
                            {
                                // 标记需要触发断开连接
                                serverDataQueue.Enqueue((MessageType.MessageDisconnect, null));
                            }
                            break; // 退出接收循环
                        }
                        
                        // 其他可恢复的错误（如网络暂时不可用）
                        Debug.LogWarning($"UDP receive socket error: {e.Message} (ErrorCode: {errorCode}), retrying...");
                        Thread.Sleep(100); // 短暂等待后重试
                        continue;
                    }

                    if (data == null || data.Length == 0)
                        continue;

                    // 将UDP数据输入到KCP
                    if (kcp != null)
                    {
                        kcp.Input(data);
                        kcp.Update(DateTimeOffset.UtcNow);

                        // 尝试从KCP接收完整的数据包
                        // 注意：TryRecv() 只有在收到完整消息（所有分片）时才会返回数据
                        // 如果消息被分片，需要等待所有分片到达
                        int recvCount = 0;
                        while (true)
                        {
                            var (buffer, length) = kcp.TryRecv();
                            if (buffer == null || length <= 0)
                            {
                                // length == -1 表示没有可用数据或分片不完整（需要等待更多数据）
                                // length == -2 表示错误
                                if (length == -2)
                                {
                                    Debug.LogWarning("[KCP] TryRecv returned error: -2");
                                }
                                else if (length == -1 && recvCount == 0)
                                {
                                    // 第一次调用 TryRecv 返回 -1，说明数据不完整或还在等待
                                    Debug.Log($"[KCP] TryRecv returned -1: waiting for more data or fragments");
                                }

                                break;
                            }

                            recvCount++;
                            try
                            {
                                // 立即复制数据，避免 buffer 被释放
                                byte[] receivedData = new byte[length];
                                var span = buffer.Memory.Span.Slice(0, length);
                                span.CopyTo(receivedData);

                                // 立即释放 buffer
                                buffer.Dispose();
                                buffer = null;

                                // 只显示前16字节的预览，避免日志过长
                                string preview = receivedData.Length <= 16
                                    ? string.Join(", ", receivedData.Select(b => b.ToString()))
                                    : string.Join(", ", receivedData.Take(16).Select(b => b.ToString())) + "...";

                                // 处理接收到的数据（可能包含多个消息）
                                ProcessKCPData(receivedData);
                            }
                            catch (ObjectDisposedException e)
                            {
                                Debug.LogError($"[KCP] Buffer was disposed before access: {e.Message}");
                            }
                            catch (Exception e)
                            {
                                Debug.LogError($"[KCP] Error processing KCP data: {e.Message}\n{e.StackTrace}");
                            }
                            finally
                            {
                                // 确保 buffer 被释放
                                if (buffer != null)
                                {
                                    try
                                    {
                                        buffer.Dispose();
                                    }
                                    catch
                                    {
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    if (isRunning)
                    {
                        Debug.LogError($"UDP receive loop error: {e.Message}\n{e.StackTrace}");
                    }

                    break;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"ReceiveLoop fatal error: {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            Debug.Log("KCP receive loop exited");
        }
    }

    /// <summary>
    /// KCP更新循环（定期调用kcp.Update()来发送握手包和保持连接）
    /// </summary>
    private void KCPUpdateLoop()
    {
        try
        {
            while (isRunning && kcp != null)
            {
                try
                {
                    // 定期更新KCP（每10ms更新一次，与interval配置一致）
                    if (kcp != null)
                    {
                        kcp.Update(DateTimeOffset.UtcNow);
                    }

                    // 等待10ms后再次更新
                    Thread.Sleep(10);
                }
                catch (Exception e)
                {
                    if (isRunning)
                    {
                        Debug.LogError($"KCP update error: {e.Message}");
                    }

                    break;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"KCP update loop fatal error: {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            Debug.Log("KCP update loop exited");
        }
    }

    /// <summary>
    /// KCP输出回调实现（将KCP数据通过UDP发送）
    /// </summary>
    private class KcpCallbackImpl : IKcpCallback
    {
        private FrameSyncNetworkKCP parent;

        public KcpCallbackImpl(FrameSyncNetworkKCP parent)
        {
            this.parent = parent;
        }

        public void Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            try
            {
                if (parent.udpClient == null || parent.serverEndPoint == null)
                {
                    buffer.Dispose();
                    return;
                }

                // 将KCP输出的数据通过UDP发送
                byte[] data = new byte[avalidLength];
                buffer.Memory.Span.Slice(0, avalidLength).CopyTo(data);
                buffer.Dispose();

                parent.udpClient.Send(data, data.Length, parent.serverEndPoint);
            }
            catch (Exception e)
            {
                Debug.LogError($"KCP Output error: {e.Message}");
            }
        }
    }
}