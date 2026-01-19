using System.Collections.Generic;
using System.Linq;
using Frame.Core;
using Frame.ECS;
using Frame.FixMath;
using Proto;
using UnityEngine;
using UnityEngine.UI; // 添加UI命名空间

public enum ConnectState
{
    TCP,
    UDP,
    KCP
}

public class ECSFrameSyncExample : SingletonMono<ECSFrameSyncExample>
{
    public ConnectState connectState = ConnectState.TCP;
    public INetwork network;
    private ECSPredictionRollbackManager ecsPredictionManager => ECSPredictionRollbackManager.Instance;

    public FrameSyncNetworkTCP FrameSyncNetworkTcp;
    public FrameSyncNetworkUDP FrameSyncNetworkUdp;
    public FrameSyncNetworkKCP FrameSyncNetworkKcp;

    [Header("玩家设置")] public GameObject playerPrefab;

    [Header("子弹设置")] public GameObject bulletPrefab; // 可选：如果不设置，会自动创建红色小球

    [Header("僵尸设置")] public GameObject zombiePrefab; // 可选：如果不设置，会自动创建绿色方块

    [Header("墙设置")] public GameObject wallPrefab; // 可选：如果不设置，会自动创建灰色方块

    [Header("UI设置")] public Text debugText; // 调试信息显示

    public GameObject myPlayer;
    public bool isSmooth;
    public float smoothNum;

    // 网络统计
    private float lastServerFrameTime;
    private float networkLatency; // 两次接收帧的时间间隔

    void Start()
    {
        switch (connectState)
        {
            case ConnectState.TCP:
                network = FrameSyncNetworkTcp;
                break;
            case ConnectState.UDP:
                network = FrameSyncNetworkUdp;
                break;
            case ConnectState.KCP:
                network = FrameSyncNetworkKcp;
                break;
        }

        // 注册事件回调
        network.OnConnected += OnConnected;
        network.OnDisconnected += OnDisconnected;
        network.OnGameStarted += OnGameStarted;
        network.OnServerFrameReceived += OnServerFrameReceived;
        network.Connect();

        // 连接服务器
    }

    public float timer = 0;
    public float timer1 = 0;

    public float sendInterval;
    public float predictInterval;

    // 输入缓冲区：保存当前的输入状态
    private InputDirection bufferedDirection = InputDirection.DirectionNone;
    private bool bufferedFire = false;
    private bool bufferedToggle = false;
    private long bufferedFireX = 0;
    private long bufferedFireY = 0;

    void Update()
    {
        if (!network.IsGameStarted)
        {
            return;
        }


        timer += Time.deltaTime;
        timer1 += Time.deltaTime;

        // 1. 检测输入并更新缓冲区（每帧都更新，保持最新状态）
        InputDirection newDirection = DetectMovementInput();
        bool fire, isToggle;
        long fireX, fireY;
        DetectFireInput(out isToggle, out fire, out fireX, out fireY);

        // 更新输入缓冲区（每帧都更新，反映当前输入状态）
        // 移动方向：如果有新输入就更新，否则保持之前的状态
        // 这样即使当前帧没有检测到新输入（可能因为检测时机问题），也会继续发送之前的移动方向
        if (newDirection != InputDirection.DirectionNone)
        {
            bufferedDirection = newDirection;
        }
        // 注意：如果当前帧没有移动输入，bufferedDirection 保持之前的值
        // 这样在 sendInterval 到达时，即使当前帧没有新输入，也会发送之前的移动状态

        // 发射/放置：如果有新输入就更新
        if (fire)
        {
            bufferedFire = true;
            bufferedFireX = fireX;
            bufferedFireY = fireY;
        }
        // 注意：如果没有按下，bufferedFire 保持之前的值
        // 这样在 sendInterval 到达时，如果之前有发射输入，也会发送

        // 切换：只在按下时更新（GetMouseButtonDown 只在按下瞬间为 true）
        if (isToggle)
        {
            bufferedToggle = true;
        }

// 3. 客户端预测（无论是否有输入，都要持续预测）
        // 原因：游戏世界在持续运行（如子弹在移动），即使本地玩家无输入
        // 使用缓冲区中的输入进行预测，确保预测和发送的输入一致
        if (timer1 > predictInterval)
        {
            timer1 = 0;
            UpdateInputStatePredict(bufferedDirection, bufferedFire, bufferedFireX, bufferedFireY, bufferedToggle);
        }

        // 2. 发送输入到服务器（定时发送，即使当前帧没有新输入也发送缓冲区中的状态）
        // 这样确保所有输入都能被发送，不会因为某帧没有新输入而丢失
        if (timer > sendInterval)
        {
            timer = 0;

            // 检查是否有任何输入需要发送
            bool hasInput = bufferedDirection != InputDirection.DirectionNone ||
                            bufferedFire ||
                            bufferedToggle;

            if (hasInput)
            {
                network.SendFrameData(bufferedDirection, bufferedFire, bufferedFireX, bufferedFireY, bufferedToggle);


                // 发送后，清除已发送的输入状态（准备下一轮缓冲）
                // 移动方向：如果发送的是 DirectionNone，保持；否则清除（因为已经发送）

                bufferedDirection = InputDirection.DirectionNone;

                // 发射：清除（因为已经发送）
                bufferedFire = false;
                bufferedFireX = 0;
                bufferedFireY = 0;

                // 切换：清除（因为切换是一次性操作）
                bufferedToggle = false;
            }
        }


        // 4. 同步ECS World状态到Unity对象（视图层）
        ECSSyncHelper.SyncFromWorldToUnity(ECSPredictionRollbackManager.Instance.currentWorld);

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
    private void DetectFireInput(out bool isToggle, out bool fire, out long fireX, out long fireY)
    {
        fire = Input.GetMouseButton(0);
        isToggle = Input.GetMouseButtonDown(1);
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

        // 获取玩家冷却信息（用于显示）
        float bulletCooldownDisplay = 0f;
        float wallCooldownDisplay = 0f;
        if (ecsPredictionManager.currentWorld != null)
        {
            int myPlayerId = network.MyID;
            var playerEntity = ECSSyncHelper.GetEntityByPlayerId(myPlayerId);
            if (playerEntity.HasValue)
            {
                if (ecsPredictionManager.currentWorld.TryGetComponent<PlayerComponent>(playerEntity.Value,
                        out var playerComp))
                {
                    bulletCooldownDisplay = (float)playerComp.bulletCooldownTimer;
                    wallCooldownDisplay = (float)playerComp.wallCooldownTimer;
                }
            }
        }

        debugText.text = $"<b>帧同步调试信息</b>\n" +
                         $"发送帧率: {1 / sendInterval} fps\n" +
                         $"预测帧率: {1 / predictInterval} fps\n" +
                         $"预测帧: {predictedFrame}\n" +
                         $"确认帧: {confirmedFrame}\n" +
                         $"<color=yellow>待确认帧数: {pendingFrames}</color>\n" +
                         $"<color=cyan>网络延迟: {networkLatency:F0} ms</color>\n" +
                         $"<color=orange>子弹冷却: {bulletCooldownDisplay:F2}s</color>\n" +
                         $"<color=orange>墙冷却: {wallCooldownDisplay:F2}s</color>\n" +
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
                ecsPredictionManager.currentWorld,
                playerId,
                player,
                startPos,
                100
            );

            if (playerId == network.MyID)
            {
                myPlayer = player;
            }
        }


        // 不存在，创建新的单例组件
        var newEntity = ecsPredictionManager.currentWorld.CreateEntity();
        var component = new GridMapComponent(20, 20, Fix64.One);
        ecsPredictionManager.currentWorld.AddComponent(newEntity, component);

        var newEntity2 = ecsPredictionManager.currentWorld.CreateEntity();
        var component2 = new FlowFieldComponent(0, null);
        ecsPredictionManager.currentWorld.AddComponent(newEntity2, component2);
        
        ecsPredictionManager.confirmedWorld = ecsPredictionManager.currentWorld.Clone();
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
            ecsPredictionManager. ProcessServerFrameNoPredict(serverFrame);
        }
    }

    /// <summary>
    /// 客户端预测：立即执行输入
    /// </summary>
    void UpdateInputStatePredict(InputDirection currentDirection, bool fire, long fireX = 0, long fireY = 0,
        bool isToggle = false)
    {
        if (ecsPredictionManager.enablePredictionRollback)
        {
            ecsPredictionManager.PredictInput(network.MyID, currentDirection, fire, fireX, fireY, isToggle);
        }
    }
}