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

    public GameObject playerPrefab;

    public GameObject myPlayer;

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
        networkManager.OnGameStarted += OnGameStarted;
        networkManager.OnServerFrameReceived += OnServerFrameReceived;

        // 连接服务器
        networkManager.Connect();
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            currentDirection = InputDirection.DirectionUp;
        }
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            currentDirection = InputDirection.DirectionDown;
        }
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            currentDirection = InputDirection.DirectionLeft;
        }
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            currentDirection = InputDirection.DirectionRight;
        }
    }

    void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnConnected -= OnConnected;
            networkManager.OnDisconnected -= OnDisconnected;
            networkManager.OnGameStarted -= OnGameStarted;
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
    /// 游戏开始回调
    /// </summary>
    private void OnGameStarted(GameStart gameStart)
    {
        Debug.Log($"Game started! Room: {gameStart.RoomId}, Random Seed: {gameStart.RandomSeed}");
        Debug.Log($"Players in game: {string.Join(", ", gameStart.PlayerIds)}");
        myPlayer = Instantiate(playerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
    }

    /// <summary>
    /// 接收到服务器帧数据回调
    /// </summary>
    private void OnServerFrameReceived(ServerFrame serverFrame)
    {
        if (currentDirection != InputDirection.DirectionNone)
        {
            networkManager.SendFrameData(currentDirection);
        }


        currentDirection = InputDirection.DirectionNone;


        // 处理所有玩家的输入数据
        foreach (var frameData in serverFrame.FrameDatas)
        {
            if (frameData.PlayerId == networkManager.myPlayerID)
            {
                UpdatePlayerState(frameData);
            }
        }
    }

    public float speed = 0.1f;

    /// <summary>
    /// 根据帧数据更新玩家状态
    /// </summary>
    private void UpdatePlayerState(FrameData frameData)
    {
        // 根据 frameData 更新对应玩家的位置、状态等
        // 这里只是示例，实际实现需要根据游戏逻辑来写


        switch (frameData.Direction)
        {
            case InputDirection.DirectionUp:
                myPlayer.transform.position += Vector3.up * speed;
                // 向上移动
                break;
            case InputDirection.DirectionDown:
                myPlayer.transform.position += Vector3.down * speed;
                // 向下移动
                break;
            case InputDirection.DirectionLeft:
                myPlayer.transform.position += Vector3.left * speed;
                // 向左移动
                break;
            case InputDirection.DirectionRight:
                myPlayer.transform.position += Vector3.right * speed;
                // 向右移动
                break;
        }
    }
}