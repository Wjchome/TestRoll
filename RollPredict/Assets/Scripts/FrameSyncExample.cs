using System.Collections.Generic;
using UnityEngine;
using Proto;

/// <summary>
/// 帧同步网络使用示例（支持预测回滚）
/// 展示如何连接服务器、发送输入、接收帧数据，并实现客户端预测和回滚
/// </summary>
public class FrameSyncExample : MonoBehaviour, IGameLogicExecutor
{
    private FrameSyncNetwork networkManager => FrameSyncNetwork.Instance;

    private PredictionRollbackManager predictionManager => PredictionRollbackManager.Instance;

    [Header("玩家设置")] public GameObject playerPrefab;
    public float speed = 0.1f;

    // 玩家对象映射
    private Dictionary<int, GameObject> playerObjects = new Dictionary<int, GameObject>();

    // 输入处理
    public InputDirection currentDirection;
    private long localFrameNumber = 0;

    public GameObject myPlayer;


    void Start()
    {
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
        if (!networkManager.isGameStarted)
            return;

        // 检测输入
        InputDirection newDirection = InputDirection.DirectionNone;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            newDirection = InputDirection.DirectionUp;
        }
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            newDirection = InputDirection.DirectionDown;
        }
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            newDirection = InputDirection.DirectionLeft;
        }
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            newDirection = InputDirection.DirectionRight;
        }


        if (newDirection != InputDirection.DirectionNone)
            currentDirection = newDirection;


        // 客户端预测：立即执行输入
        if (predictionManager != null && predictionManager.enablePredictionRollback)
        {
            localFrameNumber++;
            predictionManager.PredictInput(networkManager.myPlayerID, currentDirection, localFrameNumber);
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
    private void OnConnected(long playerID)
    {
        Debug.Log($"Connected to server! Player ID: {playerID}");
    }

    /// <summary>
    /// 断开连接回调
    /// </summary>
    private void OnDisconnected()
    {
        Debug.Log("Disconnected from server");
        if (predictionManager != null)
        {
            predictionManager.ClearHistory();
        }
    }

    /// <summary>
    /// 游戏开始回调
    /// </summary>
    private void OnGameStarted(GameStart gameStart)
    {
        Debug.Log($"Game started! Room: {gameStart.RoomId}, Random Seed: {gameStart.RandomSeed}");
        Debug.Log($"Players in game: {string.Join(", ", gameStart.PlayerIds)}");

        // 创建玩家对象
        playerObjects.Clear();
        int index = 1;
        foreach (var playerId in gameStart.PlayerIds)
        {
            Vector3 startPos = new Vector3(index++ * 2f, 0, 0);
            GameObject player = Instantiate(playerPrefab, startPos, Quaternion.identity);
            playerObjects[playerId] = player;

            // 注册到预测管理器
            if (predictionManager != null)
            {
                predictionManager.RegisterPlayer(playerId, player);
            }

            if (playerId == networkManager.myPlayerID)
            {
                myPlayer = player;
            }
        }

        // 初始化随机种子
        Random.InitState((int)gameStart.RandomSeed);

        // 保存初始状态快照
        if (predictionManager != null)
        {
            predictionManager.SaveSnapshot(0);
        }
    }

    /// <summary>
    /// 接收到服务器帧数据回调
    /// </summary>
    private void OnServerFrameReceived(ServerFrame serverFrame)
    {
        // 使用预测回滚管理器处理服务器帧
        if (predictionManager != null && predictionManager.enablePredictionRollback)
        {
            predictionManager.ProcessServerFrame(serverFrame);
        }
        else
        {
            // 如果不使用预测回滚，直接执行服务器帧
            var inputs = new Dictionary<int, InputDirection>();
            foreach (var frameData in serverFrame.FrameDatas)
            {
                inputs[frameData.PlayerId] = frameData.Direction;
            }

            ExecuteFrame(inputs, serverFrame.FrameNumber);
        }
    }

    /// <summary>
    /// 实现IGameLogicExecutor接口：执行一帧游戏逻辑
    /// </summary>
    public void ExecuteFrame(Dictionary<int, InputDirection> inputs, long frameNumber)
    {
        foreach (var kvp in inputs)
        {
            int playerId = kvp.Key;
            InputDirection direction = kvp.Value;

            if (playerObjects.ContainsKey(playerId) && playerObjects[playerId] != null)
            {
                UpdatePlayerState(playerObjects[playerId], direction);
            }
        }

        UpdateInputState();
    }

    void UpdateInputState()
    {
        // if (inputQueue.Count > 0)
        // {
        //     networkManager.SendFrameData(inputQueue.Dequeue());
        //     if (inputQueue.Count > maxInputBuffer)
        //     {
        //         in
        //     }
        // }
        networkManager.SendFrameData(currentDirection);
        currentDirection = InputDirection.DirectionNone;
    }

    /// <summary>
    /// 根据输入更新玩家状态
    /// </summary>
    private void UpdatePlayerState(GameObject player, InputDirection direction)
    {
        if (player == null)
            return;

        switch (direction)
        {
            case InputDirection.DirectionUp:
                player.transform.position += Vector3.up * speed;
                break;
            case InputDirection.DirectionDown:
                player.transform.position += Vector3.down * speed;
                break;
            case InputDirection.DirectionLeft:
                player.transform.position += Vector3.left * speed;
                break;
            case InputDirection.DirectionRight:
                player.transform.position += Vector3.right * speed;
                break;
            case InputDirection.DirectionNone:
                // 无输入，不移动
                break;
        }
    }
}