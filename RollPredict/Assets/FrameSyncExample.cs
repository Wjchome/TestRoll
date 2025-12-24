using UnityEngine;
using Proto;

/// <summary>
/// 帧同步网络使用示例
/// 展示如何连接服务器、发送输入、接收帧数据
/// </summary>
public class FrameSyncExample : MonoBehaviour
{
    private FrameSyncNetwork networkManager;
    private InputDirection currentDirection = InputDirection.DirectionNone;
    private bool isInputPressed = false;

    void Start()
    {
        // 获取或创建网络管理器
        networkManager = FindObjectOfType<FrameSyncNetwork>();
        if (networkManager == null)
        {
            GameObject networkObj = new GameObject("FrameSyncNetwork");
            networkManager = networkObj.AddComponent<FrameSyncNetwork>();
        }

        // 设置服务器信息
        networkManager.serverIP = "127.0.0.1";
        networkManager.serverPort = 8088;
        networkManager.playerName = "Player_" + Random.Range(1000, 9999);

        // 注册事件回调
        networkManager.OnConnected += OnConnected;
        networkManager.OnDisconnected += OnDisconnected;
        networkManager.OnServerFrameReceived += OnServerFrameReceived;

        // 连接服务器
        networkManager.Connect();
    }

    void Update()
    {
        // 检测输入（上下左右）
        InputDirection newDirection = InputDirection.DirectionNone;
        bool newPressed = false;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            newDirection = InputDirection.DirectionUp;
            newPressed = true;
        }
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            newDirection = InputDirection.DirectionDown;
            newPressed = true;
        }
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            newDirection = InputDirection.DirectionLeft;
            newPressed = true;
        }
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            newDirection = InputDirection.DirectionRight;
            newPressed = true;
        }


        currentDirection = newDirection;
        isInputPressed = newPressed;

        if (networkManager != null && networkManager.isConnected)
        {
            networkManager.SendFrameData(currentDirection, isInputPressed);
            
        }


        // 检测释放按键
        if (Input.GetKeyUp(KeyCode.W) || Input.GetKeyUp(KeyCode.UpArrow) ||
            Input.GetKeyUp(KeyCode.S) || Input.GetKeyUp(KeyCode.DownArrow) ||
            Input.GetKeyUp(KeyCode.A) || Input.GetKeyUp(KeyCode.LeftArrow) ||
            Input.GetKeyUp(KeyCode.D) || Input.GetKeyUp(KeyCode.RightArrow))
        {
            if (networkManager != null && networkManager.isConnected)
            {
                networkManager.SendFrameData(InputDirection.DirectionNone, false);
                currentDirection = InputDirection.DirectionNone;
                isInputPressed = false;
            }
        }
    }

    void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnConnected -= OnConnected;
            networkManager.OnDisconnected -= OnDisconnected;
            networkManager.OnServerFrameReceived -= OnServerFrameReceived;
        }
    }

    /// <summary>
    /// 连接成功回调
    /// </summary>
    private void OnConnected(string playerID)
    {
        Debug.Log($"Connected to server! Player ID: {playerID}");
    }

    /// <summary>
    /// 断开连接回调
    /// </summary>
    private void OnDisconnected()
    {
        Debug.Log("Disconnected from server");
    }

    /// <summary>
    /// 接收到服务器帧数据回调
    /// </summary>
    private void OnServerFrameReceived(ServerFrame serverFrame)
    {
        Debug.Log($"Received frame {serverFrame.FrameNumber} with {serverFrame.FrameDatas.Count} player inputs");

        // 处理所有玩家的输入数据
        foreach (var frameData in serverFrame.FrameDatas)
        {
            Debug.Log($"Player {frameData.PlayerId}: {frameData.Direction} (pressed: {frameData.IsPressed})");

            // 在这里更新游戏状态
            // 例如：移动玩家、处理碰撞等
            UpdatePlayerState(frameData);
        }
    }

    /// <summary>
    /// 根据帧数据更新玩家状态
    /// </summary>
    private void UpdatePlayerState(FrameData frameData)
    {
        // 根据 frameData 更新对应玩家的位置、状态等
        // 这里只是示例，实际实现需要根据游戏逻辑来写

        if (frameData.IsPressed)
        {
            switch (frameData.Direction)
            {
                case InputDirection.DirectionUp:
                    // 向上移动
                    break;
                case InputDirection.DirectionDown:
                    // 向下移动
                    break;
                case InputDirection.DirectionLeft:
                    // 向左移动
                    break;
                case InputDirection.DirectionRight:
                    // 向右移动
                    break;
            }
        }
    }
}