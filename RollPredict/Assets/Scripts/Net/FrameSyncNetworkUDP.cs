using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
public class FrameSyncNetworkUDP : SingletonMono<FrameSyncNetworkUDP>, INetwork
{
    [Header("服务器设置")] public string serverIP = "127.0.0.1";
    public int serverPort = 8089;
    public string playerName = "Player";

    [Header("状态")] public bool isConnected = false;
    public bool isGameStarted = false;
    public int myPlayerID;

    private UdpClient udpClient;
    private IPEndPoint serverEndPoint; // 服务器端点
    private IPEndPoint remoteEndPoint; // 接收数据时的远程端点
    private Thread receiveThread;
    private bool isRunning = false;
    private bool isConnecting = false; // 连接进行中标志，防止重复连接
    private readonly object threadLock = new object(); // 用于线程同步


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
                Debug.LogWarning(
                    $"Connection already in progress (isConnected: {isConnected}, isConnecting: {isConnecting})");
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

        serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
        remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        // 在后台线程中执行连接，避免阻塞Unity主线程
        Thread connectThread = new Thread(() =>
        {
            try
            {
                Debug.Log($"Attempting to connect to {serverIP}:{serverPort}...");
                udpClient = new UdpClient();

                udpClient.Connect(serverEndPoint); // 设置默认远程端点（非真实连接，仅简化发送）
                udpClient.Client.ReceiveTimeout = 5000; // 设置接收超时5秒
                udpClient.Client.SendTimeout = 5000; // 设置发送超时5秒

                isRunning = true;
                // 创建接收线程
                lock (threadLock)
                {
                    if (receiveThread != null && receiveThread.IsAlive)
                    {
                        isRunning = false;
                        receiveThread.Join(500);
                        receiveThread = null;
                    }

                    receiveThread = new Thread(ReceiveMessages);
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }

                // 标记业务层连接成功
                lock (threadLock)
                {
                    isConnected = true;
                    isConnecting = false;
                }

                isGameStarted = false;

                Debug.Log($"UDP client initialized for {serverIP}:{serverPort} (no real connection)");
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

        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error closing TCP client: {e.Message}");
            }

            udpClient = null;
        }

        Debug.Log("Disconnected from server");
        OnDisconnected?.Invoke();
    }


    /// <summary>
    /// 发送帧数据（上下左右）
    /// </summary>
    public void SendFrameData(InputDirection direction, bool isFire = false, long fireX = 0, long fireY = 0,
        bool isToggle = false)
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
        if (!isConnected)
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

            // 2. 打包消息类型（1字节）
            byte typeByte = (byte)messageType;

            // 3. 合并所有数据：length(4) + type(1) + data(n)
            byte[] sendBuffer = new byte[4 + 1 + data.Length];
            Buffer.BlockCopy(lengthBytes, 0, sendBuffer, 0, 4);
            sendBuffer[4] = typeByte;
            Buffer.BlockCopy(data, 0, sendBuffer, 5, data.Length);

            // TCP → UDP 修改：发送UDP数据报
            int sentBytes = udpClient.Send(sendBuffer, sendBuffer.Length);
            if (sentBytes != sendBuffer.Length)
            {
                Debug.LogWarning($"UDP send incomplete: sent {sentBytes}/{sendBuffer.Length} bytes");
            }
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
                byte[] receiveBuffer = udpClient.Receive(ref remoteEndPoint);


                // 解析消息格式：len(4) + type(1) + data(n)
                int offset = 0;

                // 1. 读取长度（4字节）
                if (receiveBuffer.Length < 4)
                {
                    Debug.LogWarning($"UDP datagram too small (length < 4): {receiveBuffer.Length} bytes");
                    continue;
                }

                Buffer.BlockCopy(receiveBuffer, offset, lengthBuffer, 0, 4);
                offset += 4;

                // 转换为大端uint
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(lengthBuffer);
                }

                uint totalLength = BitConverter.ToUInt32(lengthBuffer, 0);

                // 2. 验证总长度
                if (totalLength + 4 > receiveBuffer.Length) // 4是长度字段本身
                {
                    Debug.LogWarning(
                        $"UDP datagram length mismatch: expected {totalLength + 4}, got {receiveBuffer.Length}");
                    continue;
                }

                // 3. 读取消息类型（1字节）
                if (offset + 1 > receiveBuffer.Length)
                {
                    Debug.LogWarning("Failed to read message type from UDP datagram");
                    continue;
                }

                Buffer.BlockCopy(receiveBuffer, offset, typeBuffer, 0, 1);
                offset += 1;
                byte messageTypeByte = typeBuffer[0];
                MessageType messageType = (MessageType)messageTypeByte;

                // 4. 读取数据部分
                int dataLength = (int)totalLength - 1;
                if (dataLength < 0 || offset + dataLength > receiveBuffer.Length)
                {
                    Debug.LogWarning(
                        $"Invalid data length: {dataLength} (offset: {offset}, buffer length: {receiveBuffer.Length})");
                    continue;
                }

                byte[] dataBuffer = new byte[dataLength];
                Buffer.BlockCopy(receiveBuffer, offset, dataBuffer, 0, dataLength);

                // 5. 验证消息类型有效性
                if (!Enum.IsDefined(typeof(MessageType), messageType))
                {
                    Debug.LogWarning($"Received invalid message type byte: {messageTypeByte} (0x{messageTypeByte:X2})");
                    continue;
                }

                // 6. 处理消息
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
                Debug.LogWarning(
                    $"MESSAGE_UNKNOWN data could be ConnectMessage: playerId={connectMsg.PlayerId}, playerName={connectMsg.PlayerName}");
            }
            catch
            {
            }

            // 尝试解析为ServerFrame
            try
            {
                var serverFrame = ServerFrame.Parser.ParseFrom(data);
                Debug.LogWarning(
                    $"MESSAGE_UNKNOWN data could be ServerFrame: frameNumber={serverFrame.FrameNumber}, timestamp={serverFrame.Timestamp}, frameDatasCount={serverFrame.FrameDatas.Count}");
            }
            catch
            {
            }

            // 尝试解析为GameStart
            try
            {
                var gameStart = GameStart.Parser.ParseFrom(data);
                Debug.LogWarning(
                    $"MESSAGE_UNKNOWN data could be GameStart: roomId={gameStart.RoomId}, randomSeed={gameStart.RandomSeed}, playerIdsCount={gameStart.PlayerIds.Count}");
            }
            catch
            {
            }
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
                        serverDataQueue.Enqueue((MessageType.MessageConnect, serverFrame));
                    }
                }
                    break;

                case MessageType.MessageServerFrame:
                {
                    var serverFrame = ServerFrame.Parser.ParseFrom(data);
                    lock (queueLock)
                    {
                        serverDataQueue.Enqueue((MessageType.MessageServerFrame, serverFrame));
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
                        Debug.LogWarning(
                            $"MESSAGE_UNKNOWN data (first 50 bytes): {BitConverter.ToString(data, 0, Math.Min(50, data.Length))}");

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
                            Debug.Log(
                                $"Game started! Room: {gameStart.RoomId}, Seed: {gameStart.RandomSeed}, Players: {gameStart.PlayerIds.Count}");
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