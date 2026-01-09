using System.Collections.Generic;
using System.Linq;
using Frame.Core;
using Frame.ECS;
using Frame.ECS.Components;
using Frame.FixMath;
using Proto;
using UnityEngine;
using UnityEngine.UI;  // 添加UI命名空间

/// <summary>
/// ECS版本的帧同步示例
/// 使用ECS架构实现预测回滚
/// </summary>
public class ECSFrameSyncExample : SingletonMono<ECSFrameSyncExample>
{
    private FrameSyncNetwork networkManager => FrameSyncNetwork.Instance;
    private ECSPredictionRollbackManager ecsPredictionManager => ECSPredictionRollbackManager.Instance;

    [Header("玩家设置")]
    public GameObject playerPrefab;

    [Header("子弹设置")]
    public GameObject bulletPrefab; // 可选：如果不设置，会自动创建红色小球

    [Header("UI设置")]
    public Text debugText; // 调试信息显示

    public GameObject myPlayer;
    public bool isSmooth;
    public float smoothNum;

    // 网络统计
    private float lastServerFrameTime;
    private float networkLatency; // 两次接收帧的时间间隔

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

    public float sendInterval;
    public float predictInterval;

    void Update()
    {
        if (!networkManager.isGameStarted || ecsPredictionManager == null)
            return;

        timer += Time.deltaTime;
        timer1 += Time.deltaTime;

        // 1. 检测输入（分离输入检测和预测执行）
        InputDirection newDirection = DetectMovementInput();
        bool fire;
        long fireX, fireY;
        DetectFireInput(out fire, out fireX, out fireY);

        // 2. 发送输入到服务器（有输入时才发送）
        if ((newDirection != InputDirection.DirectionNone || fire) && timer > sendInterval)
        {
            timer = 0;
            networkManager.SendFrameData(newDirection, fire, fireX, fireY);
        }

        // 3. 客户端预测（无论是否有输入，都要持续预测）
        // 原因：游戏世界在持续运行（如子弹在移动），即使本地玩家无输入
        if (timer1 > predictInterval)
        {
            timer1 = 0;
            UpdateInputStatePredict(newDirection, fire, fireX, fireY);
        }

        // 4. 同步ECS World状态到Unity对象（视图层）
        ECSSyncHelper.SyncFromWorldToUnity(ecsPredictionManager.world);

        // 5. 更新UI调试信息
        UpdateDebugUI();
    }

    /// <summary>
    /// 检测移动输入（8个方向）
    /// </summary>
    private InputDirection DetectMovementInput()
    {
        bool up = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool down = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);

        // 检测组合按键（斜向）
        if (up && left) return InputDirection.DirectionUpLeft;
        if (up && right) return InputDirection.DirectionUpRight;
        if (down && left) return InputDirection.DirectionDownLeft;
        if (down && right) return InputDirection.DirectionDownRight;

        // 检测单一方向
        if (up) return InputDirection.DirectionUp;
        if (down) return InputDirection.DirectionDown;
        if (left) return InputDirection.DirectionLeft;
        if (right) return InputDirection.DirectionRight;

        return InputDirection.DirectionNone;
    }

    /// <summary>
    /// 检测发射输入
    /// </summary>
    private void DetectFireInput(out bool fire, out long fireX, out long fireY)
    {
        fire = Input.GetMouseButton(0);
        fireX = 0;
        fireY = 0;

        if (fire)
        {
            // 获取鼠标在世界坐标中的位置
            Vector2 mousePos = Input.mousePosition;
            Vector3 fireWorldPos = Camera.main.ScreenToWorldPoint(mousePos);
            fireX = ((Fix64)fireWorldPos.x).RawValue;
            fireY = ((Fix64)fireWorldPos.y).RawValue;
        }
    }

    /// <summary>
    /// 更新调试UI显示
    /// </summary>
    private void UpdateDebugUI()
    {
        if (debugText == null)
            return;

        long predictedFrame = ecsPredictionManager.predictedFrame;
        long confirmedFrame = ecsPredictionManager.confirmedServerFrame;
        long pendingFrames = predictedFrame - confirmedFrame;

        debugText.text = $"<b>帧同步调试信息</b>\n" +
                        $"发送帧率: {1/sendInterval} fps\n" +
                        $"预测帧率: {1/predictInterval} fps\n" +
                        $"预测帧: {predictedFrame}\n" +
                        $"确认帧: {confirmedFrame}\n" +
                        $"<color=yellow>待确认帧数: {pendingFrames}</color>\n" +
                        $"<color=cyan>网络延迟: {networkLatency:F0} ms</color>\n" +
                        $"FPS: {(1.0f / Time.deltaTime):F0}";
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
        // 计算网络延迟（两次接收帧的时间间隔）
        float currentTime = Time.realtimeSinceStartup;
        if (lastServerFrameTime > 0)
        {
            float deltaTime = currentTime - lastServerFrameTime;
            networkLatency = deltaTime * 1000f; // 转换为毫秒
        }
        lastServerFrameTime = currentTime;

        if (ecsPredictionManager.enablePredictionRollback)
        {
            ecsPredictionManager.ProcessServerFrame(serverFrame);
        }
        else
        {
            // 使用统一的状态机执行
            ecsPredictionManager.world = ECSStateMachine.Execute(
                ecsPredictionManager.world, serverFrame.FrameDatas.ToList());
        }
    }

    /// <summary>
    /// 客户端预测：立即执行输入
    /// </summary>
    void UpdateInputStatePredict(InputDirection currentDirection, bool fire, long fireX = 0, long fireY = 0)
    {
        if (ecsPredictionManager.enablePredictionRollback)
        {
            // 先预测，让玩家立即看到效果
            ecsPredictionManager.PredictInput(networkManager.myPlayerID, currentDirection, fire, fireX, fireY);
        }
    }
}