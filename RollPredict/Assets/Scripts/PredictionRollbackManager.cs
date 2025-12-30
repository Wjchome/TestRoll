using System;
using System.Collections.Generic;
using Frame.Core;
using UnityEngine;
using Proto;

/// <summary>
/// 预测回滚管理器
/// 实现客户端预测和服务器回滚机制
/// </summary>
public class PredictionRollbackManager : SingletonMono<PredictionRollbackManager>
{
    [Header("配置")]
    [Tooltip("最大保存的快照数量")]
    public int maxSnapshots = 100;
    
    [Tooltip("是否启用预测回滚")]
    public bool enablePredictionRollback = true;

    // 快照历史（按帧号索引）状态
    private Dictionary<long, GameStateSnapshot> snapshotHistory = new Dictionary<long, GameStateSnapshot>();

    // 输入历史（按帧号索引）输入
    private Dictionary<long, Dictionary<int, InputDirection>> inputHistory = new Dictionary<long, Dictionary<int, InputDirection>>();

    // 当前确认的服务器帧号
    private long confirmedServerFrame = -1;

    // 当前预测的帧号
    private long predictedFrame = 0;

    // 玩家对象映射
    private Dictionary<int, GameObject> playerObjects = new Dictionary<int, GameObject>();

    // 游戏逻辑执行器
    private IGameLogicExecutor gameLogicExecutor;

    // 事件回调
    public System.Action<long> OnRollback;
    public System.Action<long> OnPrediction;

    void Start()
    {
        gameLogicExecutor = GetComponent<IGameLogicExecutor>();
        if (gameLogicExecutor == null)
        {
            Debug.LogError("PredictionRollbackManager requires an IGameLogicExecutor component!");
        }
    }

    /// <summary>
    /// 注册玩家对象
    /// </summary>
    public void RegisterPlayer(int playerId, GameObject playerObject)
    {
        playerObjects[playerId] = playerObject;
    }

    /// <summary>
    /// 保存当前帧的状态快照
    /// </summary>
    public void SaveSnapshot(long frameNumber)
    {
        if (!enablePredictionRollback)
            return;

        var snapshot = new GameStateSnapshot(frameNumber);
        
        foreach (var kvp in playerObjects)
        {
            var playerId = kvp.Key;
            var playerObj = kvp.Value;
            if (playerObj != null)
            {
                snapshot.playerStates[playerId] = new PlayerState(
                    playerId,
                    playerObj.transform.position,
                    playerObj.transform.rotation
                );
            }
        }

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
        
        foreach (var kvp in snapshot.playerStates)
        {
            var playerId = kvp.Key;
            var playerState = kvp.Value;
            
            if (playerObjects.ContainsKey(playerId) && playerObjects[playerId] != null)
            {
                playerObjects[playerId].transform.position = playerState.position;
                playerObjects[playerId].transform.rotation = playerState.rotation;
            }
        }
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

    /// <summary>
    /// 客户端预测：立即执行输入
    /// </summary>
    public void PredictInput(int playerId, InputDirection direction, long frameNumber)
    {
        if (!enablePredictionRollback)
            return;

        // 如果还没有保存前一帧的快照，先保存
        if (frameNumber > 0 && !snapshotHistory.ContainsKey(frameNumber - 1))
        {
            SaveSnapshot(frameNumber - 1);
        }

        // 保存输入
        SaveInput(frameNumber, playerId, direction);

        // 执行预测
        if (gameLogicExecutor != null)
        {
            var inputs = new Dictionary<int, InputDirection> { { playerId, direction } };
            gameLogicExecutor.ExecuteFrame(inputs, frameNumber);
        }

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
            // 如果不启用预测回滚，直接执行服务器帧
            if (gameLogicExecutor != null)
            {
                var inputs = new Dictionary<int, InputDirection>();
                foreach (var frameData in serverFrame.FrameDatas)
                {
                    inputs[frameData.PlayerId] = frameData.Direction;
                }
                gameLogicExecutor.ExecuteFrame(inputs, serverFrame.FrameNumber);
            }
            confirmedServerFrame = serverFrame.FrameNumber;
            return;
        }

        long serverFrameNumber = serverFrame.FrameNumber;

        // 如果服务器帧号小于等于已确认的帧号，忽略（可能是重复或延迟的帧）
        if (serverFrameNumber <= confirmedServerFrame)
        {
            return;
        }

        // 检查是否需要回滚
        bool needRollback = false;
        long rollbackToFrame = confirmedServerFrame;

        // 如果服务器帧号跳跃了（大于预测帧+1），需要回滚
        if (serverFrameNumber > predictedFrame + 1)
        {
            needRollback = true;
            rollbackToFrame = confirmedServerFrame;
        }
        // 如果服务器帧号在预测范围内，检查输入是否一致
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
                    var predictedInput = inputHistory[serverFrameNumber][playerId];
                    if (predictedInput != serverInput)
                    {
                        needRollback = true;
                        rollbackToFrame = Math.Max(rollbackToFrame, serverFrameNumber - 1);
                    }
                }
            }
        }

        // 执行回滚
        if (needRollback && rollbackToFrame >= 0 && snapshotHistory.ContainsKey(rollbackToFrame))
        {
            RollbackToFrame(rollbackToFrame);
            OnRollback?.Invoke(rollbackToFrame);
        }

        // 保存服务器确认的输入
        foreach (var frameData in serverFrame.FrameDatas)
        {
            SaveInput(serverFrameNumber, frameData.PlayerId, frameData.Direction);
        }

        // 从回滚点重新执行到当前服务器帧
        for (long frame = rollbackToFrame + 1; frame <= serverFrameNumber; frame++)
        {
            var inputs = GetInputs(frame);
            if (gameLogicExecutor != null)
            {
                gameLogicExecutor.ExecuteFrame(inputs, frame);
            }
            SaveSnapshot(frame);
        }

        // 继续预测未来的帧（如果有本地输入）
        confirmedServerFrame = serverFrameNumber;
        predictedFrame = serverFrameNumber;

        // 预测未来帧（如果有未确认的输入）
        PredictFutureFrames();
    }

    /// <summary>
    /// 回滚到指定帧
    /// </summary>
    private void RollbackToFrame(long frameNumber)
    {
        if (frameNumber < 0 || !snapshotHistory.ContainsKey(frameNumber))
        {
            Debug.LogWarning($"Cannot rollback to frame {frameNumber}");
            return;
        }

        LoadSnapshot(frameNumber);
    }

    /// <summary>
    /// 预测未来帧（基于本地输入）
    /// </summary>
    private void PredictFutureFrames()
    {
        // 这里可以添加逻辑来预测未来帧
        // 例如：如果客户端有未发送的输入，可以提前预测
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

/// <summary>
/// 游戏逻辑执行器接口
/// 实现此接口以定义游戏逻辑如何执行
/// </summary>
public interface IGameLogicExecutor
{
    /// <summary>
    /// 执行一帧游戏逻辑
    /// </summary>
    /// <param name="inputs">该帧所有玩家的输入</param>
    /// <param name="frameNumber">帧号</param>
    void ExecuteFrame(Dictionary<int, InputDirection> inputs, long frameNumber);
}

