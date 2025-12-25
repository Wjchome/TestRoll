using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using Google.Protobuf;
using Proto;

/// <summary>
/// 帧同步网络管理器
/// 处理格式：len(4 bytes) + messageType(1 byte) + byte[]
/// </summary>
public class FrameSyncNetwork : MonoBehaviour
{
    [Header("服务器设置")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 8088;
    public string playerName = "Player";

    [Header("状态")]
    public bool isConnected = false;
    public bool isGameStarted = false;
    public int myPlayerID ;

    private TcpClient tcpClient;
    private NetworkStream stream;
    private Thread receiveThread;
    private bool isRunning = false;

    // 消息队列（线程安全）
    private Queue<(MessageType, IMessage)> serverFrameQueue = new Queue<(MessageType, IMessage)>();
    private object queueLock = new object();

    // 当前帧号
    private long currentFrameNumber = 0;

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
    /// 连接到服务器
    /// </summary>
    public void Connect()
    {
        if (isConnected)
        {
            Debug.LogWarning("Already connected to server");
            return;
        }

        try
        {
            tcpClient = new TcpClient();
            tcpClient.Connect(serverIP, serverPort);
            tcpClient.NoDelay = true; // 禁用Nagle算法，减少延迟

            stream = tcpClient.GetStream();
            isRunning = true;
            isConnected = true;
            isGameStarted = false; // 重置游戏状态

            // 启动接收线程
            receiveThread = new Thread(ReceiveMessages);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            Debug.Log($"Connected to server {serverIP}:{serverPort}");

        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to server: {e.Message}");
            isConnected = false;
            isGameStarted = false;
        }
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public void Disconnect()
    {
        isRunning = false;
        isConnected = false;
        isGameStarted = false;

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(1000);
        }

        if (stream != null)
        {
            stream.Close();
            stream = null;
        }

        if (tcpClient != null)
        {
            tcpClient.Close();
            tcpClient = null;
        }

        Debug.Log("Disconnected from server");
        OnDisconnected?.Invoke();
    }


    /// <summary>
    /// 发送帧数据（上下左右）
    /// </summary>
    public void SendFrameData(InputDirection direction)
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
            FrameNumber = currentFrameNumber
        };
        SendMessage(MessageType.MessageFrameData, frameData);
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
                MessageType messageType = (MessageType)typeBuffer[0];

                // 读取数据部分
                int dataLength = (int)totalLength - 1;
                if (dataLength < 0)
                {
                    Debug.LogWarning("Invalid message length");
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
                            serverFrameQueue.Enqueue((MessageType.MessageConnect,serverFrame));
                        }
                    }
                    break;

                case MessageType.MessageServerFrame:
                    {
                        var serverFrame = ServerFrame.Parser.ParseFrom(data);
                        lock (queueLock)
                        {
                            serverFrameQueue.Enqueue((MessageType.MessageServerFrame,serverFrame));
                        }
                    }
                    break;

                case MessageType.MessageDisconnect:
                    {
                        var disconnectMsg = DisconnectMessage.Parser.ParseFrom(data);
                        lock (queueLock)
                        {
                            serverFrameQueue.Enqueue((MessageType.MessageDisconnect, disconnectMsg));
                        }
                    }
                    break;

                case MessageType.MessageGameStart:
                    {
                        var gameStart = GameStart.Parser.ParseFrom(data);
                        lock (queueLock)
                        {
                            serverFrameQueue.Enqueue((MessageType.MessageGameStart, gameStart));
                        }
                    }
                    break;

                default:
                    Debug.LogWarning($"Unknown message type: {messageType}");
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
            while (serverFrameQueue.Count > 0)
            {
                var serverFrame = serverFrameQueue.Dequeue();
                switch (serverFrame.Item1)
                {
                    case MessageType.MessageConnect:
                        
                        if (serverFrame.Item2 is ConnectMessage connectMessage)
                        {
                            myPlayerID = connectMessage.PlayerId;	
                            OnConnected.Invoke(connectMessage.PlayerId);
                            
                        }
                        break;
                    case MessageType.MessageServerFrame:
                        if (serverFrame.Item2 is ServerFrame serverFrame2)
                        {
                            currentFrameNumber = serverFrame2.FrameNumber;
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
                            if (serverFrame.Item2 is GameStart gameStart)
                            {
                                isGameStarted = true;
                                OnGameStarted?.Invoke(gameStart);
                                Debug.Log($"Game started! Room: {gameStart.RoomId}, Seed: {gameStart.RandomSeed}, Players: {gameStart.PlayerIds.Count}");
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
