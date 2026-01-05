using System;
using System.Collections.Generic;
using Frame.Core;
using Frame.FixMath;
using UnityEngine;
using Proto;
using UnityEditor;

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
    public long confirmedServerFrame = 0;

    // 当前预测的帧号
    public long predictedFrame = 0;

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

    public Vector3 offset = new Vector3(0, 2, 0); // 数字相对于物体的偏移（避免重叠）
    public Color textColor = Color.white; // 文字颜色
    public float textSize = 15; // 文字大小

    private void OnDrawGizmos()
    {
        Vector3 drawPos = transform.position + offset;

        // 3. 保存当前GUI样式，避免影响全局
        GUI.skin.label.fontSize = (int)textSize;
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            normal = { textColor = textColor }, // 设置文字颜色
            alignment = TextAnchor.MiddleCenter // 文字居中
        };
        // 4. 绘制数字（核心：Handles.Label显示文字，Gizmos负责辅助图形）
        Handles.Label(drawPos, Math.Max(0, predictedFrame - confirmedServerFrame).ToString(), style);

        // 可选：绘制一个小原点，标记数字对应的位置（便于定位）
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 0.1f);
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
    /// 保存当前帧的状态快照,将currentGameState存到这个frameNumber里去
    /// </summary>
    public void SaveSnapshot(long frameNumber, GameState saveGameState)
    {
        if (!enablePredictionRollback)
            return;

        // 从当前GameState创建快照
        saveGameState.frameNumber = frameNumber;
        GameState snapshot = saveGameState.Clone();
        snapshot.frameNumber = frameNumber;
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
    public GameState LoadSnapshot(long frameNumber)
    {
        if (!snapshotHistory.ContainsKey(frameNumber))
        {
            Debug.LogWarning($"Snapshot for frame {frameNumber} not found!");
            return null;
        }

        var snapshot = snapshotHistory[frameNumber];

        return snapshot.Clone();
    }


    /// <summary>
    /// 保存输入到历史记录
    /// </summary>
    public void SaveInput(long frameNumber, ServerFrame serverFrame)
    {
        if (!enablePredictionRollback)
            return;


        inputHistory[frameNumber] = new Dictionary<int, InputDirection>();


        foreach (var frame in serverFrame.FrameDatas)
        {
            inputHistory[frameNumber][frame.PlayerId] = frame.Direction;
        }
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

        // 保存输入（这个输入是预测的，可能会被服务器覆盖）
        SaveInput(frameNumber, new ServerFrame()
        {
            FrameDatas = { new FrameData() { PlayerId = playerId, Direction = direction } },
        });

        // 使用统一的状态机执行预测
        // State(n+1) = StateMachine(State(n), Input(n))
        var inputs = new Dictionary<int, InputDirection> { { playerId, direction } };
        currentGameState = StateMachine.Execute(currentGameState, inputs);

        // 保存预测后的状态快照
        SaveSnapshot(frameNumber, currentGameState);

        predictedFrame = Math.Max(predictedFrame, frameNumber);
        OnPrediction?.Invoke(frameNumber);
    }

    public enum NetState
    {
        NoPredictionAndLose, //无预测并且丢包
        NoPredictionAndSuccess, //无预测并且成功
        Repeat, //重复包
        PredictAndLose, //预测并且丢包
        PredictAndSuccessAndInputOk, //预测并且成功 并且预测成功
        PredictAndSuccessAndInputFail, //预测并且成功 并且预测失败
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
        NetState currentNetState = NetState.NoPredictionAndLose;

        // 比如发消息时帧 1，预测帧 2 ，收到消息帧1 ，重复接受
        if (serverFrameNumber <= confirmedServerFrame)
        {
            currentNetState = NetState.Repeat;
            return;
        }

        if (predictedFrame <= confirmedServerFrame)
        {
            if (serverFrameNumber > confirmedServerFrame + 1)
            {
                currentNetState = NetState.NoPredictionAndLose;
            }
            else
            {
                currentNetState = NetState.NoPredictionAndSuccess;
            }
        }
        else
        {
            if (serverFrameNumber > confirmedServerFrame + 1)
            {
                currentNetState = NetState.PredictAndLose;
            }
            else
            {
                bool needRollback = false;

                if (serverFrame.FrameDatas.Count != GetInputs(serverFrameNumber).Count)
                {
                    needRollback = true;
                }

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
                            break;
                        }
                    }
                    else
                    {
                        needRollback = true;
                        break;
                    }
                }

                if (needRollback)
                {
                    currentNetState = NetState.PredictAndSuccessAndInputFail;
                }
                else
                {
                    currentNetState = NetState.PredictAndSuccessAndInputOk;
                }
            }
        }

        switch (currentNetState)
        {
            // NoPredictionAndLose, //无预测并且丢包
            // NoPredictionAndSuccess, //无预测并且成功
            // Repeat, //重复包
            // PredictAndLose, //预测并且丢包
            // PredictAndSuccessAndInputOk, //预测并且成功 并且预测成功
            // PredictAndSuccessAndInputFail, //预测并且成功 并且预测失败
            
            case NetState.Repeat:
                //暂时这样，丢包需要请求
                break;
            case NetState.NoPredictionAndLose:
            case NetState.PredictAndLose:
                FrameSyncNetwork.Instance.SendLossFrame(confirmedServerFrame);
                break;
            case NetState.NoPredictionAndSuccess:
                
                SaveInput(serverFrameNumber, serverFrame);
                var inputs = GetInputs(serverFrameNumber);
                currentGameState = StateMachine.Execute(currentGameState, inputs);
                SaveSnapshot(serverFrameNumber, currentGameState);
                confirmedServerFrame = Math.Max(confirmedServerFrame, serverFrameNumber);
                //predictedFrameIndex = Math.Max(1, predictedFrame - confirmedServerFrame + 1);
                predictedFrameIndex = 1;
                break;

            case NetState.PredictAndSuccessAndInputOk:
                Debug.Log("PredictAndSuccessAndInputOk");
                confirmedServerFrame = Math.Max(confirmedServerFrame, serverFrameNumber);
                //predictedFrameIndex = Math.Max(1, predictedFrame - confirmedServerFrame + 1);
                predictedFrameIndex = 1;
                break;

            case NetState.PredictAndSuccessAndInputFail:
                Debug.Log("PredictAndSuccessAndInputFail");

                SaveInput(serverFrameNumber, serverFrame);


                currentGameState =  LoadSnapshot(confirmedServerFrame);
                for (long frame = confirmedServerFrame; frame <= predictedFrame; frame++)
                {
                    var newInputs = GetInputs(frame);
                    currentGameState = StateMachine.Execute(currentGameState, newInputs);
                    SaveSnapshot(frame, currentGameState);
                }
                confirmedServerFrame = Math.Max(confirmedServerFrame, serverFrameNumber);
                //predictedFrameIndex = Math.Max(1, predictedFrame - confirmedServerFrame + 1);
                predictedFrameIndex = 1;
                break;
        }

        
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