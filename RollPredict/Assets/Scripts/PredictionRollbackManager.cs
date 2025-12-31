using System;
using System.Collections.Generic;
using Frame.Core;
using Frame.FixMath;
using UnityEngine;
using Proto;

/// <summary>
/// 预测回滚管理器
/// 实现客户端预测和服务器回滚机制
/// </summary>
public class PredictionRollbackManager : SingletonMono<PredictionRollbackManager>
{
    [Header("配置")] [Tooltip("最大保存的快照数量")] public int maxSnapshots = 100;

    [Tooltip("是否启用预测回滚")] public bool enablePredictionRollback = true;

    // 快照历史（按帧号索引）状态 - 使用统一的GameState
    private Dictionary<long, GameState> snapshotHistory = new Dictionary<long, GameState>();

    // 输入历史（按帧号索引）输入
    private Dictionary<long, Dictionary<int, InputDirection>> inputHistory =
        new Dictionary<long, Dictionary<int, InputDirection>>();

    // 当前确认的服务器帧号
    private long confirmedServerFrame = -1;

    // 当前预测的帧号
    private long predictedFrame = 0;

    // 玩家对象映射（视图层）
    public Dictionary<int, GameObject> playerObjects = new Dictionary<int, GameObject>();


    // 当前游戏状态（统一的状态机框架使用）
    public GameState currentGameState = new GameState(0);

    // 事件回调
    public System.Action<long> OnRollback;
    public System.Action<long> OnPrediction;

    void Start()
    {
        // 不再需要IGameLogicExecutor，直接使用StateMachine
    }

    /// <summary>
    /// 注册玩家对象
    /// </summary>
    public void RegisterPlayer(int playerId, GameObject playerObject, FixVector3 position)
    {
        playerObjects[playerId] = playerObject;

        // 同步到GameState
        var playerState = currentGameState.GetOrCreatePlayer(playerId);
        playerState.position = position;
    }

    /// <summary>
    /// 保存当前帧的状态快照
    /// </summary>
    public void SaveSnapshot(long frameNumber)
    {
        if (!enablePredictionRollback)
            return;

        // 从当前GameState创建快照
        currentGameState.frameNumber = frameNumber;
        GameState snapshot = currentGameState.Clone();
        snapshotHistory[frameNumber] = snapshot;

        // 清理旧的快照（保留最近maxSnapshots个）
        if (snapshotHistory.Count > maxSnapshots)
        {
            var framesToRemove = new List<long>();
            var sortedFrames = new List<long>(snapshotHistory.Keys);
            sortedFrames.Sort();

            int removeCount = sortedFrames.Count - maxSnapshots;
            for (int i = 0; i < removeCount; i++)
            {
                framesToRemove.Add(sortedFrames[i]);
            }

            foreach (var frame in framesToRemove)
            {
                snapshotHistory.Remove(frame);
                inputHistory.Remove(frame);
            }
        }
    }

    /// <summary>
    /// 加载指定帧的状态快照
    /// </summary>
    public void LoadSnapshot(long frameNumber)
    {
        if (!enablePredictionRollback)
            return;

        if (!snapshotHistory.ContainsKey(frameNumber))
        {
            Debug.LogWarning($"Snapshot for frame {frameNumber} not found!");
            return;
        }

        var snapshot = snapshotHistory[frameNumber];

        // 恢复GameState
        currentGameState = snapshot.Clone();
    }


    /// <summary>
    /// 保存输入到历史记录
    /// </summary>
    public void SaveInput(long frameNumber, int playerId, InputDirection direction)
    {
        if (!enablePredictionRollback)
            return;

        if (!inputHistory.ContainsKey(frameNumber))
        {
            inputHistory[frameNumber] = new Dictionary<int, InputDirection>();
        }

        inputHistory[frameNumber][playerId] = direction;
    }

    /// <summary>
    /// 获取指定帧的输入
    /// </summary>
    public Dictionary<int, InputDirection> GetInputs(long frameNumber)
    {
        if (inputHistory.ContainsKey(frameNumber))
        {
            return new Dictionary<int, InputDirection>(inputHistory[frameNumber]);
        }

        return new Dictionary<int, InputDirection>();
    }
    public long predictedFrameIndex = 1;
    /// <summary>
    /// 客户端预测：立即执行输入
    /// 使用统一的状态机框架：State(n+1) = StateMachine(State(n), Input(n))
    /// </summary>
    public void PredictInput(int playerId, InputDirection direction)
    {
        if (!enablePredictionRollback)
            return;
        long frameNumber = confirmedServerFrame + predictedFrameIndex++;
        // 如果还没有保存前一帧的快照，先保存
        if (frameNumber > 0 && !snapshotHistory.ContainsKey(frameNumber - 1))
        {
            SaveSnapshot(frameNumber - 1);
        }

        // 保存输入（这个输入是预测的，可能会被服务器覆盖）
        SaveInput(frameNumber, playerId, direction);

        // 使用统一的状态机执行预测
        // State(n+1) = StateMachine(State(n), Input(n))
        var inputs = new Dictionary<int, InputDirection> { { playerId, direction } };
        currentGameState = StateMachine.Execute(currentGameState, inputs);

        // 保存预测后的状态快照
        SaveSnapshot(frameNumber);

        predictedFrame = Math.Max(predictedFrame, frameNumber);
        OnPrediction?.Invoke(frameNumber);
    }

    /// <summary>
    /// 处理服务器帧：检查是否需要回滚
    /// </summary>
    public void ProcessServerFrame(ServerFrame serverFrame)
    {
        if (!enablePredictionRollback)
        {
            return;
        }

        long serverFrameNumber = serverFrame.FrameNumber;

        // 比如发消息时帧 1，预测帧 2 ，收到消息帧1 ，重复接受
        if (serverFrameNumber <= confirmedServerFrame)
        {
            return;
        }

        // 检查是否需要回滚
        bool needRollback = false;
        long rollbackToFrame = confirmedServerFrame;
        // 比如发消息时帧 1，预测帧 2 ，收到消息帧3 ，这他妈是少接受了一帧，需要请求 2，3，稍后处理
        if (serverFrameNumber > predictedFrame)
        {
            needRollback = true;
            rollbackToFrame = confirmedServerFrame;
        }
        // 比如发消息时帧 1，预测帧 2 ，收到消息帧2 ，需要对比一下帧是否正确 ，不一样则用服务器的，回滚一帧
        // 发消息时帧    1，预测帧 3 ，收到消息帧2 ，不一样则用服务器的，然后重新预测帧3
        else if (serverFrameNumber <= predictedFrame)
        {
            // 检查服务器帧中的输入是否与本地预测一致
            foreach (var serverFrameData in serverFrame.FrameDatas)
            {
                var playerId = serverFrameData.PlayerId;
                var serverInput = serverFrameData.Direction;

                if (inputHistory.ContainsKey(serverFrameNumber) &&
                    inputHistory[serverFrameNumber].ContainsKey(playerId))
                {
                    InputDirection predictedInput = inputHistory[serverFrameNumber][playerId];
                    if (predictedInput != serverInput)
                    {
                        needRollback = true;
                        rollbackToFrame = Math.Max(rollbackToFrame, serverFrameNumber - 1);
                        break;
                    }
                }
                else
                {
                    needRollback = true;
                    rollbackToFrame = Math.Max(rollbackToFrame, serverFrameNumber - 1);
                    break;
                }
            }
        }

        // 运行到这里的时候，confirmedServerFrame = 1  rollbackToFrame = 1 predictedFrame = 2  serverFrameNumber = 2 
        // 运行到这里的时候，confirmedServerFrame = 1  rollbackToFrame = 1 predictedFrame = 3  serverFrameNumber = 2 
        // 执行回滚 复原状态到 rollbackToFrame
        if (needRollback && rollbackToFrame >= 0 && snapshotHistory.ContainsKey(rollbackToFrame))
        {
            LoadSnapshot(rollbackToFrame);
            OnRollback?.Invoke(rollbackToFrame);
        }

        // 保存服务器确认的输入
        foreach (var frameData in serverFrame.FrameDatas)
        {
            SaveInput(serverFrameNumber, frameData.PlayerId, frameData.Direction);
        }

        // 从回滚点重新执行到当前预测帧
        // 使用统一的状态机框架：State(n+1) = StateMachine(State(n), Input(n))
        for (long frame = rollbackToFrame + 1; frame <= predictedFrame; frame++)
        {
            var inputs = GetInputs(frame);
            // 使用统一的状态机执行
            currentGameState = StateMachine.Execute(currentGameState, inputs);
            
            SaveSnapshot(frame);
        }

        // 继续预测未来的帧（如果有本地输入）
        confirmedServerFrame = serverFrameNumber;
        //比如只预测1帧，那么这个直接回归到1。如果预测帧3 确定帧 2，那么往后预测则是 预测帧 4
        predictedFrameIndex = predictedFrame  - confirmedServerFrame + 1;
        
    }


    /// <summary>
    /// 获取当前确认的服务器帧号
    /// </summary>
    public long GetConfirmedFrame()
    {
        return confirmedServerFrame;
    }

    /// <summary>
    /// 获取当前预测的帧号
    /// </summary>
    public long GetPredictedFrame()
    {
        return predictedFrame;
    }

    /// <summary>
    /// 清理历史数据
    /// </summary>
    public void ClearHistory()
    {
        snapshotHistory.Clear();
        inputHistory.Clear();
        confirmedServerFrame = -1;
        predictedFrame = 0;
    }
}