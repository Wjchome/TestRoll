using System.Collections.Generic;
using Frame.FixMath;
using Frame.Physics2D;
using UnityEngine;
using Proto;

/// <summary>
/// 帧同步网络使用示例（支持预测回滚）
/// 展示如何连接服务器、发送输入、接收帧数据，并实现客户端预测和回滚
/// 使用统一的状态机框架：State(n+1) = StateMachine(State(n), Input(n))
/// </summary>
public class FrameSyncExample : MonoBehaviour
{
    private FrameSyncNetwork networkManager => FrameSyncNetwork.Instance;

    private PredictionRollbackManager predictionManager => PredictionRollbackManager.Instance;

    [Header("玩家设置")] public GameObject playerPrefab;


    public GameObject myPlayer;

    void Start()
    {
        // 设置服务器信息

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

    public bool isSmooth;

    public float timer = 0;
    public float timer1 = 0;


    void Update()
    {
        if (!networkManager.isGameStarted)
            return;
        timer += Time.deltaTime;
        timer1 += Time.deltaTime;
        foreach (var kvp in predictionManager.playerObjects)
        {
            int id = kvp.Key;

            // if (isSmooth)
            // {
            //     kvp.Value.transform.position = Vector3.Lerp(kvp.Value.transform.position,
            //         (Vector3)predictionManager.currentGameState.[id].position, Time.deltaTime * smoothTime);
            // }
            // else
            // {
            //     if (predictionManager.currentGameState.players.TryGetValue(id, out PlayerState playerState))
            //     {
            //         kvp.Value.transform.position = (Vector3)playerState.position;
            //     }
            // }
        }

        // 检测输入（8个方向）
        InputDirection newDirection = InputDirection.DirectionNone;
        
        bool up = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool down = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        
        // 检测组合按键（斜向）
        if (up && left)
        {
            newDirection = InputDirection.DirectionUpLeft;
        }
        else if (up && right)
        {
            newDirection = InputDirection.DirectionUpRight;
        }
        else if (down && left)
        {
            newDirection = InputDirection.DirectionDownLeft;
        }
        else if (down && right)
        {
            newDirection = InputDirection.DirectionDownRight;
        }
        // 检测单一方向
        else if (up)
        {
            newDirection = InputDirection.DirectionUp;
        }
        else if (down)
        {
            newDirection = InputDirection.DirectionDown;
        }
        else if (left)
        {
            newDirection = InputDirection.DirectionLeft;
        }
        else if (right)
        {
            newDirection = InputDirection.DirectionRight;
        }


        // 只收集输入，不立即预测
        // 预测将在收到服务器帧确认后，在UpdateInputState中执行
        if (newDirection != InputDirection.DirectionNone)
        { 
            if (timer > 0.05f)
            {
                timer = 0;
                // 然后发送输入到服务器
                networkManager.SendFrameData(newDirection);
            }

            if (timer1 > 0.03f)
            {
                timer1 = 0;
                UpdateInputStatePredict(newDirection);
            }
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
            var pos = new FixVector2(index, Fix64.Zero);
            index += Fix64.One;
            FixVector2 startPos = new FixVector2(index,  Fix64.Zero);
            GameObject player = Instantiate(playerPrefab, (Vector2)startPos, Quaternion.identity);
            RigidBody2DComponent playerRigidbody = player.GetComponent<RigidBody2DComponent>();
            PhysicsWorld2DComponent.Instance.AddRigidBody( playerRigidbody, startPos,PhysicsLayer.Everything);

            predictionManager.RegisterPlayer(playerId, player, playerRigidbody);


            if (playerId == networkManager.myPlayerID)
            {
                myPlayer = player;
            }
        }

        // 初始化随机种子
        Random.InitState((int)gameStart.RandomSeed);
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
            // 使用统一的状态机框架：State(n+1) = StateMachine(State(n), Input(n))
            var inputs = new Dictionary<int, InputDirection>();
            foreach (var frameData in serverFrame.FrameDatas)
            {
                inputs[frameData.PlayerId] = frameData.Direction;
            }

            // 使用统一的状态机执行
            predictionManager.currentGameState = StateMachine.Execute(
                predictionManager.currentGameState, inputs);
        }
    }


    void UpdateInputStatePredict(InputDirection currentDirection)
    {
        // 如果启用预测回滚，先执行客户端预测（在发送前）
        if (predictionManager != null && predictionManager.enablePredictionRollback)
        {
            // 先预测，让玩家立即看到效果
            predictionManager.PredictInput(networkManager.myPlayerID, currentDirection);
        }
    }
}