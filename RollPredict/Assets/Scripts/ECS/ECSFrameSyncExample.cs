using System.Collections.Generic;
using Frame.Core;
using Frame.ECS;
using Frame.ECS.Components;
using Frame.FixMath;
using Proto;
using UnityEngine;

/// <summary>
/// ECS版本的帧同步示例
/// 使用ECS架构实现预测回滚
/// </summary>
public class ECSFrameSyncExample :SingletonMono<ECSFrameSyncExample>
{
    private FrameSyncNetwork networkManager => FrameSyncNetwork.Instance;
    private ECSPredictionRollbackManager ecsPredictionManager => ECSPredictionRollbackManager.Instance;

    [Header("玩家设置")]
    public GameObject playerPrefab;

    public GameObject myPlayer;

    void Start()
    {
        // 确保这是唯一的实例
        if (Instance != this)
        {
            Debug.LogWarning($"Start() called on non-singleton instance of {GetType().Name}. Ignoring.");
            return;
        }

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

    public float timer = 0;
    public float timer1 = 0;

    void Update()
    {
        if (!networkManager.isGameStarted || ecsPredictionManager == null)
            return;

        timer += Time.deltaTime;
        timer1 += Time.deltaTime;

        // 检测移动输入（8个方向）
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

        // 检测发射输入（空格键）
        bool fire = Input.GetKeyDown(KeyCode.Space);

        // 发送输入到服务器
        if (newDirection != InputDirection.DirectionNone || fire)
        {
            if (timer > 0.05f)
            {
                timer = 0;
                // 发送移动输入到服务器
                if (newDirection != InputDirection.DirectionNone)
                {
                    networkManager.SendFrameData(newDirection);
                }
            }

            // 客户端预测
            if (timer1 > 0.04f)
            {
                timer1 = 0;
                UpdateInputStatePredict(newDirection, fire);
            }
        }

        // 同步ECS World状态到Unity对象（视图层）
        ECSSyncHelper.SyncFromWorldToUnity(ecsPredictionManager.world);
    }

    /// <summary>
    /// 连接成功回调
    /// </summary>
    private void OnConnected(long a)
    {
        Debug.Log("Connected to server!");
    }

    /// <summary>
    /// 断开连接回调
    /// </summary>
    private void OnDisconnected()
    {
        Debug.Log("Disconnected from server!");
    }

    /// <summary>
    /// 游戏开始回调
    /// </summary>
    private void OnGameStarted(GameStart gameStart)
    {
        Debug.Log($"Game started! Room: {gameStart.RoomId}, Random Seed: {gameStart.RandomSeed}");
        Debug.Log($"Players in game: {string.Join(", ", gameStart.PlayerIds)}");

        // 初始化玩家
        Fix64 index = Fix64.Zero;
        foreach (var playerId in gameStart.PlayerIds)
        {
            index += Fix64.One;
            FixVector2 startPos = new FixVector2(index, Fix64.Zero);

            // 1. 实例化玩家对象
            GameObject player = Instantiate(playerPrefab, (Vector2)startPos, Quaternion.identity);
            

            // 3. 注册玩家到ECS系统
            var entity = ECSSyncHelper.RegisterPlayer(
                ecsPredictionManager.world,
                playerId,
                player,
                startPos,
                100
            );

            if (playerId == networkManager.myPlayerID)
            {
                myPlayer = player;
            }
        }

        // 4. 保存初始状态快照
        ecsPredictionManager.SaveSnapshot(0);

        // 初始化随机种子
        Random.InitState((int)gameStart.RandomSeed);
    }

    /// <summary>
    /// 接收到服务器帧数据回调
    /// </summary>
    private void OnServerFrameReceived(ServerFrame serverFrame)
    {
        // 使用ECS预测回滚管理器处理服务器帧
        if (ecsPredictionManager != null && ecsPredictionManager.enablePredictionRollback)
        {
            ecsPredictionManager.ProcessServerFrame(serverFrame);
        }
    }

    /// <summary>
    /// 客户端预测：立即执行输入
    /// </summary>
    void UpdateInputStatePredict(InputDirection currentDirection, bool fire)
    {
        if (ecsPredictionManager != null && ecsPredictionManager.enablePredictionRollback)
        {
            // 先预测，让玩家立即看到效果
            ecsPredictionManager.PredictInput(networkManager.myPlayerID, currentDirection, fire);
        }
    }
}

