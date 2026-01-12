using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Frame.Core;
using Frame.ECS;
using UnityEngine;
using Google.Protobuf;
using Proto;

/// <summary>
/// 帧同步网络管理器（KCP版本）
/// 使用KCP协议替代TCP，提供更低的延迟
/// 
/// 依赖：需要添加kcp2k库
/// 安装方法：
/// 1. 通过Unity Package Manager添加Git URL: https://github.com/vis2k/kcp2k.git
/// 2. 或者下载kcp2k.dll并放到Plugins文件夹
/// </summary>
public class FrameSyncNetworkKCP : SingletonMono<FrameSyncNetworkKCP>
{
    [Header("服务器设置")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 8088;  // KCP端口
    public string playerName = "Player";

    [Header("KCP配置")]
    [Tooltip("发送窗口大小")]
    public uint sendWindowSize = 32;
    [Tooltip("接收窗口大小")]
    public uint receiveWindowSize = 32;
    [Tooltip("最大传输单元")]
    public uint mtu = 1400;
    [Tooltip("快速重传阈值")]
    public uint fastResend = 2;
    [Tooltip("无延迟模式")]
    public bool noDelay = true;
    [Tooltip("内部更新间隔（毫秒）")]
    public uint interval = 10;
    [Tooltip("快速重传触发阈值")]
    public uint resend = 2;
    [Tooltip("最小RTO（毫秒）")]
    public uint minRto = 30;

    [Header("状态")]
    public bool isConnected = false;
    public bool isGameStarted = false;
    public int myPlayerID;

    // KCP客户端（需要kcp2k库）
   //  private KcpClient kcpClient;  // 取消注释当添加kcp2k库后
    
    private Thread receiveThread;
    private bool isRunning = false;
    private bool isConnecting = false;
    private readonly object threadLock = new object();

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

    void Update()
    {
        // 在主线程中处理消息队列
        ProcessMessageQueue();
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
                Debug.LogWarning($"Connection already in progress (isConnected: {isConnected}, isConnecting: {isConnecting})");
                return;
            }
            isConnecting = true;
        }

        // 在后台线程中执行连接
        Thread connectThread = new Thread(() =>
        {
            try
            {
                Debug.Log($"Attempting to connect to KCP server {serverIP}:{serverPort}...");

                // TODO: 初始化KCP客户端
                // 需要添加kcp2k库后才能使用
                /*
                kcpClient = new KcpClient(
                    (ArraySegment<byte> data) => {
                        // 发送回调
                        // 通过UDP发送数据
                    },
                    (ArraySegment<byte> data) => {
                        // 接收回调
                        // 处理接收到的数据
                        OnKCPDataReceived(data);
                    }
                );

                // 配置KCP参数
                kcpClient.NoDelay = noDelay;
                kcpClient.Interval = (int)interval;
                kcpClient.FastResend = (int)fastResend;
                kcpClient.SendWindowSize = (int)sendWindowSize;
                kcpClient.ReceiveWindowSize = (int)receiveWindowSize;
                kcpClient.Mtu = (int)mtu;
                kcpClient.MinRto = (int)minRto;

                // 连接到服务器
                kcpClient.Connect(serverIP, serverPort);
                */

                // 临时实现：使用UDP Socket（需要手动实现KCP）
                // 这里提供一个基础框架，实际需要集成KCP库
                Debug.LogWarning("KCP库未集成，请先添加kcp2k库");
                
                lock (threadLock)
                {
                    isConnected = false;
                    isConnecting = false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"KCP Connection error: {e.Message}");
                lock (threadLock)
                {
                    isConnected = false;
                    isConnecting = false;
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
        lock (threadLock)
        {
            if (!isConnected && !isConnecting)
                return;

            isRunning = false;
            isConnecting = false;
            isConnected = false;
        }

        // TODO: 关闭KCP连接
        // if (kcpClient != null)
        // {
        //     kcpClient.Disconnect();
        //     kcpClient = null;
        // }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(1000);
            receiveThread = null;
        }

        OnDisconnected?.Invoke();
        Debug.Log("Disconnected from KCP server");
    }

    /// <summary>
    /// 发送帧数据
    /// </summary>
    public void SendFrameData(FrameData frameData)
    {
        if (!isConnected)
        {
            Debug.LogWarning("Not connected to server");
            return;
        }

        try
        {
            // 序列化消息
            byte[] data = frameData.ToByteArray();
            
            // 消息格式：len(4 bytes) + messageType(1 byte) + data
            byte[] lengthBytes = BitConverter.GetBytes((uint)(1 + data.Length));
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);

            byte[] messageTypeBytes = new byte[] { (byte)MessageType.MessageFrameData };

            // 组合消息
            byte[] message = new byte[4 + 1 + data.Length];
            Buffer.BlockCopy(lengthBytes, 0, message, 0, 4);
            Buffer.BlockCopy(messageTypeBytes, 0, message, 4, 1);
            Buffer.BlockCopy(data, 0, message, 5, data.Length);

            // TODO: 通过KCP发送
            // kcpClient.Send(new ArraySegment<byte>(message));
        }
        catch (Exception e)
        {
            Debug.LogError($"SendFrameData error: {e.Message}");
        }
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
            var frameLoss = new GetLossFrame(){ LastFrameNumber = frameNumber };
            byte[] data = frameLoss.ToByteArray();

            byte[] lengthBytes = BitConverter.GetBytes((uint)(1 + data.Length));
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);

            byte[] messageTypeBytes = new byte[] { (byte)MessageType.MessageFrameLoss };

            byte[] message = new byte[4 + 1 + data.Length];
            Buffer.BlockCopy(lengthBytes, 0, message, 0, 4);
            Buffer.BlockCopy(messageTypeBytes, 0, message, 4, 1);
            Buffer.BlockCopy(data, 0, message, 5, data.Length);

            // TODO: 通过KCP发送
            // kcpClient.Send(new ArraySegment<byte>(message));
        }
        catch (Exception e)
        {
            Debug.LogError($"SendLossFrame error: {e.Message}");
        }
    }

    /// <summary>
    /// KCP数据接收回调
    /// </summary>
    private void OnKCPDataReceived(ArraySegment<byte> data)
    {
        try
        {
            using (MemoryStream stream = new MemoryStream(data.Array, data.Offset, data.Count))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                // 读取消息长度
                byte[] lengthBytes = reader.ReadBytes(4);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(lengthBytes);
                uint length = BitConverter.ToUInt32(lengthBytes, 0);

                // 读取消息类型
                MessageType messageType = (MessageType)reader.ReadByte();

                // 读取数据
                int dataLength = (int)length - 1;
                byte[] messageData = reader.ReadBytes(dataLength);

                // 解析消息
                IMessage message = null;
                switch (messageType)
                {
                    case MessageType.MessageConnect:
                        message = ConnectMessage.Parser.ParseFrom(messageData);
                        break;
                    case MessageType.MessageServerFrame:
                        message = ServerFrame.Parser.ParseFrom(messageData);
                        break;
                    case MessageType.MessageGameStart:
                        message = GameStart.Parser.ParseFrom(messageData);
                        break;
                    default:
                        Debug.LogWarning($"Unknown message type: {messageType}");
                        return;
                }

                // 加入消息队列（在主线程处理）
                lock (queueLock)
                {
                    serverDataQueue.Enqueue((messageType, message));
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"OnKCPDataReceived error: {e.Message}");
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
                    isConnected = true;
                    OnConnected?.Invoke(connectMsg.PlayerId);
                    Debug.Log($"Connected to KCP server, PlayerID: {myPlayerID}");
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

            default:
                Debug.LogWarning($"Unhandled message type: {messageType}");
                break;
        }
    }
}

