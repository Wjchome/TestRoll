using System.Collections.Generic;
using Frame.FixMath;
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
    public Fix64 speedF => (Fix64)speed;



    // 输入处理
    public InputDirection currentDirection;
    private long lastConfirmedFrame = -1; // 最后确认的服务器帧号

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
    public float smoothTime = 5;
    void Update()
    {
        if (!networkManager.isGameStarted)
            return;

        foreach (var kvp in predictionManager.playerObjects)
        {
            int id = kvp.Key;
            kvp.Value.transform.position =Vector3.Lerp(kvp.Value.transform.position,  (Vector3)predictionManager.player2Pos[id],Time.deltaTime * smoothTime);
            //kvp.Value.transform.position = (Vector3)predictionManager.player2Pos[id];
        }
        
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


        // 只收集输入，不立即预测
        // 预测将在收到服务器帧确认后，在UpdateInputState中执行
        if (newDirection != InputDirection.DirectionNone)
            currentDirection = newDirection;
        if (canSend)
        {
            UpdateInputStatePredict();

            // 然后发送输入到服务器
            networkManager.SendFrameData(currentDirection);

            // 重置输入状态，等待下一帧收集新输入
            currentDirection = InputDirection.DirectionNone;

            canSend = false;
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


        Fix64 index = Fix64.Zero;
        foreach (var playerId in gameStart.PlayerIds)
        {
            var pos= new FixVector3(index, Fix64.Zero, Fix64.Zero);
            index += Fix64.One;
            Vector3 startPos = new Vector3((int)index, 0, 0);
            GameObject player = Instantiate(playerPrefab, startPos, Quaternion.identity);


            predictionManager.RegisterPlayer(playerId, player, pos);


            if (playerId == networkManager.myPlayerID)
            {
                myPlayer = player;
            }
        }

        // 初始化随机种子
        Random.InitState((int)gameStart.RandomSeed);

        // 初始化帧号（从0开始，第一帧将是1）
        lastConfirmedFrame = 0;

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
        // 更新最后确认的帧号
        lastConfirmedFrame = serverFrame.FrameNumber;

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

        UpdateInputState();
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

            
                UpdatePlayerState(playerId, direction);
            
        }
    }

    void UpdateInputStatePredict()
    {
        // 如果启用预测回滚，先执行客户端预测（在发送前）
        if (predictionManager != null && predictionManager.enablePredictionRollback)
        {
            // 使用下一帧号进行预测（基于最后确认的服务器帧号）
            long nextFrameNumber = lastConfirmedFrame + 1;
            // 先预测，让玩家立即看到效果
            predictionManager.PredictInput(networkManager.myPlayerID, currentDirection, nextFrameNumber);
        }
    }

    private bool canSend = false;

    /// <summary>
    /// 更新输入状态：先预测，再发送输入
    /// 确保一帧只发送一次输入
    /// 预测回滚的核心：在发送前就预测，让玩家感觉操作是即时的
    /// </summary>
    void UpdateInputState()
    {
        canSend = true;
    }

    /// <summary>
    /// 根据输入更新玩家状态
    /// </summary>
    private void UpdatePlayerState(int player, InputDirection direction)
    {
        switch (direction)
        {
            case InputDirection.DirectionUp:
                predictionManager.player2Pos[player] += FixVector3.Up * speedF;
                break;
            case InputDirection.DirectionDown:
                predictionManager.player2Pos[player] += FixVector3.Down * speedF;
                break;
            case InputDirection.DirectionLeft:
                predictionManager.player2Pos[player] += FixVector3.Left * speedF;
                break;
            case InputDirection.DirectionRight:
                predictionManager.player2Pos[player] += FixVector3.Right * speedF;
                break;
            case InputDirection.DirectionNone:
                // 无输入，不移动
                break;
        }
    }
}