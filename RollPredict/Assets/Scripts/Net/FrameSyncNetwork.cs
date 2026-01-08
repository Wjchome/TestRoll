using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using Frame.Core;
using Frame.ECS;
using UnityEngine;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Proto;

/// <summary>
/// 帧同步网络管理器
/// 处理格式：len(4 bytes) + messageType(1 byte) + byte[]
/// </summary>
public class FrameSyncNetwork :SingletonMono<FrameSyncNetwork>
{
    [Header("服务器设置")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 8089;
    public string playerName = "Player";

    [Header("状态")]
    public bool isConnected = false;
    public bool isGameStarted = false;
    public int myPlayerID ;

    private TcpClient tcpClient;
    private NetworkStream stream;
    private Thread receiveThread;
    private bool isRunning = false;
    private bool isConnecting = false; // 连接进行中标志，防止重复连接
    private readonly object threadLock = new object(); // 用于线程同步

    // 消息队列（线程安全）
    private Queue<(MessageType, IMessage)> serverDataQueue = new Queue<(MessageType, IMessage)>();
    private object queueLock = new object();
    

    // 事件回调
    public System.Action<ServerFrame> OnServerFrameReceived;
    public System.Action<long> OnConnected;
    public System.Action<GameStart> OnGameStarted;
    public System.Action OnDisconnected;
    
    

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

    /// <summary>
    /// 连接到服务器（异步，不阻塞主线程）
    /// </summary>
    public void Connect()
    {
        // 确保这是唯一的实例
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

        // 防止重复连接（使用锁保护）
        lock (threadLock)
        {
            if (isConnected || isConnecting)
            {
                Debug.LogWarning($"Connection already in progress (isConnected: {isConnected}, isConnecting: {isConnecting})");
                return;
            }
            isConnecting = true; // 标记连接开始
        }

        // 如果接收线程还在运行，先清理（使用锁确保线程安全）
        lock (threadLock)
        {
            if (receiveThread != null && receiveThread.IsAlive)
            {
                Debug.LogWarning("Receive thread is still running, cleaning up...");
                isRunning = false;
                try
                {
                    receiveThread.Join(1000); // 等待最多1秒
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Error joining receive thread: {e.Message}");
                }
                receiveThread = null;
            }
        }

        // 清理旧的连接资源（确保完全释放）
        if (stream != null)
        {
            try 
            { 
                stream.Close(); 
                stream.Dispose();
            } 
            catch { }
            stream = null;
        }

        if (tcpClient != null)
        {
            try 
            { 
                tcpClient.Close(); 
                tcpClient.Dispose();
            } 
            catch { }
            tcpClient = null;
        }

        // 等待一小段时间，确保资源完全释放
        Thread.Sleep(100);

        // 在后台线程中执行连接，避免阻塞Unity主线程
        Thread connectThread = new Thread(() =>
        {
            try
            {
                Debug.Log($"Attempting to connect to {serverIP}:{serverPort}...");
                tcpClient = new TcpClient();
                
                // 设置连接超时（5秒）
                // 注意：TcpClient没有直接的超时设置，需要通过BeginConnect + 超时来实现
                // 这里使用简单的同步连接，但在后台线程中执行，不会阻塞主线程
                var connectResult = tcpClient.BeginConnect(serverIP, serverPort, null, null);
                
                // 等待连接完成，最多等待5秒
                bool connected = connectResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                
                if (!connected)
                {
                    tcpClient.Close();
                    Debug.LogError($"Connection timeout after 5 seconds");
                    lock (threadLock)
                    {
                        isConnected = false;
                        isConnecting = false; // 连接超时，清除连接中标志
                    }
                    isGameStarted = false;
                    return;
                }
                
                tcpClient.EndConnect(connectResult);
                tcpClient.NoDelay = true; // 禁用Nagle算法，减少延迟

                stream = tcpClient.GetStream();
                isRunning = true;
                
                lock (threadLock)
                {
                    isConnected = true;
                    isConnecting = false; // 连接完成，清除连接中标志
                }
                isGameStarted = false; // 重置游戏状态

                // 确保接收线程不存在或已停止，然后创建新的接收线程（使用锁确保线程安全）
                lock (threadLock)
                {
                    // 如果接收线程仍然存在，强制停止它（可能是清理操作还没完成）
                    if (receiveThread != null && receiveThread.IsAlive)
                    {
                        Debug.LogWarning("Receive thread still exists, forcing cleanup...");
                        isRunning = false; // 先停止运行标志
                        try
                        {
                            receiveThread.Join(500); // 等待最多500ms
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"Error joining old receive thread: {e.Message}");
                        }
                        receiveThread = null;
                    }

                    // 创建新的接收线程
                    receiveThread = new Thread(ReceiveMessages);
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }

                Debug.Log($"Connected to server {serverIP}:{serverPort}");

            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to connect to server: {e.Message}");
                lock (threadLock)
                {
                    isConnected = false;
                    isConnecting = false; // 连接失败，清除连接中标志
                }
                isGameStarted = false;
                isRunning = false;
                
                // 清理资源
                if (receiveThread != null)
                {
                    receiveThread = null;
                }
                
                if (stream != null)
                {
                    try { stream.Close(); } catch { }
                    stream = null;
                }
                
                if (tcpClient != null)
                {
                    try { tcpClient.Close(); } catch { }
                    tcpClient = null;
                }
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
        isRunning = false;
        lock (threadLock)
        {
            isConnected = false;
            isConnecting = false; // 断开连接，清除连接中标志
        }
        isGameStarted = false;

        // 停止接收线程（使用锁确保线程安全）
        lock (threadLock)
        {
            if (receiveThread != null)
            {
                if (receiveThread.IsAlive)
                {
                    try
                    {
                        receiveThread.Join(1000); // 等待最多1秒
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Error joining receive thread: {e.Message}");
                    }
                }
                receiveThread = null;
            }
        }

        // 关闭流
        if (stream != null)
        {
            try
            {
                stream.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error closing stream: {e.Message}");
            }
            stream = null;
        }

        // 关闭TCP客户端
        if (tcpClient != null)
        {
            try
            {
                tcpClient.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error closing TCP client: {e.Message}");
            }
            tcpClient = null;
        }

        Debug.Log("Disconnected from server");
        OnDisconnected?.Invoke();
    }


    /// <summary>
    /// 发送帧数据（上下左右）
    /// </summary>
    public void SendFrameData(InputDirection direction, bool isFire = false, long fireX = 0, long fireY = 0)
    {
        if (!isConnected || !isGameStarted)
        {
            // 游戏未开始，不发送帧数据
            return;
        }

        var frameData = new FrameData
        {
            PlayerId = myPlayerID,
            Direction = direction,
            FrameNumber = ECSPredictionRollbackManager.Instance.confirmedServerFrame,
            IsFire = isFire
        };
        
        // 如果发射，设置目标位置
        if (isFire)
        {
            frameData.FireX = fireX;
            frameData.FireY = fireY;
        }
        
        SendMessage(MessageType.MessageFrameData, frameData);
    }

    public void SendLossFrame(long confirmedFrame)
    {
        var data = new GetLossFrame
        {
            LastFrameNumber = confirmedFrame,
        };
        Debug.Log($"Lossing frame {confirmedFrame}");
        SendMessage(MessageType.MessageFrameLoss, data);
    }

    /// <summary>
    /// 发送消息（格式：len + messageType + byte[]）
    /// </summary>
    private void SendMessage(MessageType messageType, IMessage msg)
    {
        if (!isConnected || stream == null)
            return;

        try
        {
            // 序列化 protobuf 消息
            byte[] data = msg.ToByteArray();

            // 计算总长度：1 byte (messageType) + data length
            uint totalLength = (uint)(1 + data.Length);

            // 写入长度 (4 bytes, big endian)
            byte[] lengthBytes = BitConverter.GetBytes(totalLength);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }
            stream.Write(lengthBytes, 0, 4);

            // 写入消息类型 (1 byte)
            stream.WriteByte((byte)messageType);

            // 写入数据
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to send message: {e.Message}");
            Disconnect();
        }
        
    }


    /// <summary>
    /// 接收消息线程
    /// </summary>
    private void ReceiveMessages()
    {
        byte[] lengthBuffer = new byte[4];
        byte[] typeBuffer = new byte[1];

        while (isRunning && isConnected)
        {
            try
            {
                // 读取消息长度 (4 bytes)
                int bytesRead = stream.Read(lengthBuffer, 0, 4);
                if (bytesRead != 4)
                {
                    if (bytesRead == 0)
                    {
                        // 连接已关闭
                        break;
                    }
                    Debug.LogWarning("Failed to read message length");
                    continue;
                }

                // 保存原始长度字节（用于调试）
                byte[] originalLengthBytes = new byte[4];
                Array.Copy(lengthBuffer, originalLengthBytes, 4);

                // 转换为 big endian uint32
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(lengthBuffer);
                }
                uint totalLength = BitConverter.ToUInt32(lengthBuffer, 0);


                // 读取消息类型 (1 byte)
                bytesRead = stream.Read(typeBuffer, 0, 1);
                if (bytesRead != 1)
                {
                    Debug.LogWarning("Failed to read message type");
                    continue;
                }
                byte messageTypeByte = typeBuffer[0];
                MessageType messageType = (MessageType)messageTypeByte;
                
                // 调试：记录接收到的消息类型（仅在调试模式下）
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (messageType == MessageType.MessageUnknown)
                {
                    Debug.LogWarning($"Received MESSAGE_UNKNOWN (0) - TotalLength: {totalLength}, DataLength: {totalLength - 1}, " +
                        $"OriginalLengthBytes: {BitConverter.ToString(originalLengthBytes)}, " +
                        $"ReversedLengthBytes: {BitConverter.ToString(lengthBuffer)}, " +
                        $"MessageTypeByte: 0x{messageTypeByte:X2}");
                }
                #endif
              

                // 读取数据部分
                int dataLength = (int)totalLength - 1;
                if (dataLength < 0)
                {
                    Debug.LogWarning("Invalid message length");
                    continue;
                }
                  
                // 验证消息类型是否有效
                if (!Enum.IsDefined(typeof(MessageType), messageType))
                {
                    Debug.LogWarning($"Received invalid message type byte: {messageTypeByte} (0x{messageTypeByte:X2}), TotalLength: {totalLength}");
                    // 跳过这个无效消息，尝试读取下一个消息
                    if (dataLength > 0)
                    {
                        // 跳过数据部分
                        byte[] skipBuffer = new byte[dataLength];
                        int skipped = 0;
                        while (skipped < dataLength)
                        {
                            int read = stream.Read(skipBuffer, skipped, dataLength - skipped);
                            if (read == 0) break;
                            skipped += read;
                        }
                    }
                    continue;
                }

                byte[] dataBuffer = new byte[dataLength];
                bytesRead = 0;
                while (bytesRead < dataLength)
                {
                    int read = stream.Read(dataBuffer, bytesRead, dataLength - bytesRead);
                    if (read == 0)
                    {
                        break;
                    }
                    bytesRead += read;
                }

                if (bytesRead != dataLength)
                {
                    Debug.LogWarning($"Failed to read complete message data. Expected: {dataLength}, Got: {bytesRead}");
                    continue;
                }

                // 调试：如果消息类型是0，检查数据的前几个字节，看是否是消息长度
                // 这可能是消息流不同步的迹象
                bool streamDesynchronized = false;
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (messageType == MessageType.MessageUnknown && dataLength >= 4)
                {
                    // 检查前4个字节是否是消息长度（big endian）
                    byte[] first4Bytes = new byte[4];
                    Array.Copy(dataBuffer, 0, first4Bytes, 0, 4);
                    // 转换为big endian
                    byte[] first4BytesBE = new byte[4];
                    Array.Copy(first4Bytes, first4BytesBE, 4);
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(first4BytesBE);
                    }
                    uint possibleLength = BitConverter.ToUInt32(first4BytesBE, 0);
                    if (possibleLength >= 2 && possibleLength <= 1024 * 1024)
                    {
                        streamDesynchronized = true;
                        Debug.LogError($"MESSAGE_UNKNOWN data starts with what looks like a message length: {possibleLength} (0x{possibleLength:X8}). " +
                            $"This suggests message stream desynchronization! " +
                            $"The message type byte (0x{messageTypeByte:X2}) might be wrong. " +
                            $"First 8 bytes of data: {BitConverter.ToString(dataBuffer, 0, Math.Min(8, dataLength))}");
                        
                        // 尝试修复：如果第5个字节看起来像是有效的消息类型，可能是读取位置错误
                        if (dataLength >= 5)
                        {
                            byte possibleMessageType = dataBuffer[4];
                            if (Enum.IsDefined(typeof(MessageType), (MessageType)possibleMessageType) && possibleMessageType != 0)
                            {
                                Debug.LogWarning($"Possible fix: Byte at position 4 (0x{possibleMessageType:X2}) looks like a valid message type: {(MessageType)possibleMessageType}. " +
                                    $"The message type might have been read from the wrong position!");
                                
                                // 尝试使用正确的消息类型重新解析
                                MessageType correctedMessageType = (MessageType)possibleMessageType;
                                byte[] correctedData = new byte[dataLength - 5]; // 跳过前5个字节（4字节长度+1字节类型）
                                Array.Copy(dataBuffer, 5, correctedData, 0, correctedData.Length);
                                
                                Debug.LogWarning($"Attempting to process with corrected message type: {correctedMessageType}, data length: {correctedData.Length}");
                                ProcessMessage(correctedMessageType, correctedData);
                                continue; // 跳过原始处理
                            }
                        }
                    }
                }
                #endif

                // 如果消息流不同步，可能需要断开重连
                if (streamDesynchronized)
                {
                    Debug.LogError("Message stream desynchronized! Disconnecting and reconnecting...");
                    Disconnect();
                    // 可以选择自动重连
                    // Connect();
                    break;
                }

                // 处理消息
                ProcessMessage(messageType, dataBuffer);
            }
            catch (Exception e)
            {
                if (isRunning)
                {
                    Debug.LogError($"Error receiving message: {e.Message}");
                }
                break;
            }
        }

        // 连接断开
        if (isRunning)
        {
            isRunning = false;
            isConnected = false;
          
        }
    }

    /// <summary>
    /// 尝试将MESSAGE_UNKNOWN的数据解析为已知消息类型（用于调试）
    /// </summary>
    private void TryParseAsKnownMessage(byte[] data)
    {
        try
        {
            // 尝试解析为ConnectMessage
            try
            {
                var connectMsg = ConnectMessage.Parser.ParseFrom(data);
                Debug.LogWarning($"MESSAGE_UNKNOWN data could be ConnectMessage: playerId={connectMsg.PlayerId}, playerName={connectMsg.PlayerName}");
            }
            catch { }

            // 尝试解析为ServerFrame
            try
            {
                var serverFrame = ServerFrame.Parser.ParseFrom(data);
                Debug.LogWarning($"MESSAGE_UNKNOWN data could be ServerFrame: frameNumber={serverFrame.FrameNumber}, timestamp={serverFrame.Timestamp}, frameDatasCount={serverFrame.FrameDatas.Count}");
            }
            catch { }

            // 尝试解析为GameStart
            try
            {
                var gameStart = GameStart.Parser.ParseFrom(data);
                Debug.LogWarning($"MESSAGE_UNKNOWN data could be GameStart: roomId={gameStart.RoomId}, randomSeed={gameStart.RandomSeed}, playerIdsCount={gameStart.PlayerIds.Count}");
            }
            catch { }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to parse MESSAGE_UNKNOWN data as known message types: {e.Message}");
        }
    }

    /// <summary>
    /// 处理接收到的消息
    /// </summary>
    private void ProcessMessage(MessageType messageType, byte[] data)
    {
        try
        {
            switch (messageType)
            {
                case MessageType.MessageConnect:
                    {
                        var serverFrame = ConnectMessage.Parser.ParseFrom(data);
                        lock (queueLock)
                        {
                            serverDataQueue.Enqueue((MessageType.MessageConnect,serverFrame));
                        }
                    }
                    break;

                case MessageType.MessageServerFrame:
                    {
                        var serverFrame = ServerFrame.Parser.ParseFrom(data);
                        lock (queueLock)
                        {
                            serverDataQueue.Enqueue((MessageType.MessageServerFrame,serverFrame));
                        }
                    }
                    break;

                case MessageType.MessageDisconnect:
                    {
                        var disconnectMsg = DisconnectMessage.Parser.ParseFrom(data);
                        lock (queueLock)
                        {
                            serverDataQueue.Enqueue((MessageType.MessageDisconnect, disconnectMsg));
                        }
                    }
                    break;

                case MessageType.MessageGameStart:
                    {
                        var gameStart = GameStart.Parser.ParseFrom(data);
                        lock (queueLock)
                        {
                            serverDataQueue.Enqueue((MessageType.MessageGameStart, gameStart));
                        }
                    }
                    break;
                case MessageType.MessageFrameNeed:
                {
                    var allFrame = SendAllFrame.Parser.ParseFrom(data);
                    lock (queueLock)
                    {
                        serverDataQueue.Enqueue((MessageType.MessageFrameNeed, allFrame));
                    }
                }
                    break;

                case MessageType.MessageUnknown:
                    // MESSAGE_UNKNOWN (0) 是默认值，通常不应该收到
                    // 可能的原因：
                    // 1. 消息长度读取错误，导致读取位置偏移
                    // 2. 服务器发送了未初始化的消息类型
                    // 3. 数据损坏或读取错误
                    // 4. 连接建立时的残留数据
                    // 
                    // 尝试解析数据，看是否是其他消息类型的内容
                    Debug.LogWarning($"Received MESSAGE_UNKNOWN (0) with data length {data.Length}, ignoring...");
                    
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (data.Length > 0 && data.Length <= 100) // 只记录小数据包
                    {
                        Debug.LogWarning($"MESSAGE_UNKNOWN data (first 50 bytes): {BitConverter.ToString(data, 0, Math.Min(50, data.Length))}");
                        
                        // 尝试解析为常见的消息类型，看是否能识别
                        TryParseAsKnownMessage(data);
                    }
                    #endif
                    break;

                case MessageType.MessageFrameData:
                    // MESSAGE_FRAME_DATA (2) 是客户端发送给服务器的，服务器不应该发送给客户端
                    Debug.LogWarning($"Received MESSAGE_FRAME_DATA (2) from server, this should not happen!");
                    break;

                case MessageType.MessageFrameLoss:
                    // MESSAGE_FRAME_LOSS (6) 是客户端发送给服务器的，服务器不应该发送给客户端
                    Debug.LogWarning($"Received MESSAGE_FRAME_LOSS (6) from server, this should not happen!");
                    break;

                default:
                    Debug.LogWarning($"Unknown message type: {messageType} (value: {(int)messageType})");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to parse message: {e.Message}");
        }
    }

    /// <summary>
    /// 在主线程中处理服务器帧数据
    /// </summary>
    void Update()
    {
        // 处理服务器帧数据队列
        lock (queueLock)
        {
            while (serverDataQueue.Count > 0)
            {
                var serverData = serverDataQueue.Dequeue();
                switch (serverData.Item1)
                {
                    case MessageType.MessageConnect:
                        
                        if (serverData.Item2 is ConnectMessage connectMessage)
                        {
                            myPlayerID = connectMessage.PlayerId;	
                            OnConnected.Invoke(connectMessage.PlayerId);
                            
                        }
                        break;
                    case MessageType.MessageServerFrame:
                        if (serverData.Item2 is ServerFrame serverFrame2)
                        {
                            OnServerFrameReceived?.Invoke(serverFrame2);
                        }
                        break;
                    case MessageType.MessageDisconnect:
                        {
                            isGameStarted = false;
                            OnDisconnected?.Invoke();
                        }
                        break;

                    case MessageType.MessageGameStart:
                        {
                            if (serverData.Item2 is GameStart gameStart)
                            {
                                isGameStarted = true;
                                OnGameStarted?.Invoke(gameStart);
                                Debug.Log($"Game started! Room: {gameStart.RoomId}, Seed: {gameStart.RandomSeed}, Players: {gameStart.PlayerIds.Count}");
                            }
                        }
                        break;
                    case MessageType.MessageFrameNeed:
                        {
                            if (serverData.Item2 is SendAllFrame allFrame)
                            {
                                // 遍历所有补发的帧数据
                                foreach (var serverFrame in allFrame.AllNeedFrame)
                                {
                                    // 触发帧接收回调
                                    OnServerFrameReceived?.Invoke(serverFrame);
                                }
                            }
                        }
                        break;
                }
            }
        }
    }

    /// <summary>
    /// 发送断开连接消息
    /// </summary>
    public void SendDisconnect()
    {
        if (isConnected)
        {
            var disconnectMsg = new DisconnectMessage
            {
                PlayerId = myPlayerID
            };
            SendMessage(MessageType.MessageDisconnect, disconnectMsg);
        }
        Disconnect();
    }
}
