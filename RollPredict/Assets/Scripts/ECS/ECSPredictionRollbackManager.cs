using System;
using System.Collections.Generic;
using Frame.Core;
using Frame.ECS;
using Proto;
using UnityEngine;

namespace Frame.ECS
{
    /// <summary>
    /// ECS版本的预测回滚管理器
    /// 使用ECS World存储游戏状态
    /// </summary>
    public class ECSPredictionRollbackManager :SingletonMono<ECSPredictionRollbackManager>
    {
        [Header("配置")]
        [Tooltip("最大保存的快照数量")]
        public int maxSnapshots = 100;

        [Tooltip("是否启用预测回滚")]
        public bool enablePredictionRollback = true;

        /// <summary>
        /// ECS World：存储所有游戏状态
        /// </summary>
        public World world = new World();

        /// <summary>
        /// 快照历史（按帧号索引）
        /// </summary>
        private Dictionary<long, ECSGameState> snapshotHistory = new Dictionary<long, ECSGameState>();

        /// <summary>
        /// 输入历史（按帧号索引）
        /// </summary>
        private Dictionary<long, Dictionary<int, InputDirection>> inputHistory =
            new Dictionary<long, Dictionary<int, InputDirection>>();

        /// <summary>
        /// 发射输入历史（按帧号索引）
        /// </summary>
        private Dictionary<long, Dictionary<int, bool>> fireInputHistory =
            new Dictionary<long, Dictionary<int, bool>>();

        /// <summary>
        /// 当前确认的服务器帧号
        /// </summary>
        public long confirmedServerFrame = 0;

        /// <summary>
        /// 当前预测的帧号
        /// </summary>
        public long predictedFrame = 0;

        /// <summary>
        /// 当前游戏状态（ECS版本）
        /// </summary>
        public ECSGameState currentGameState;

        /// <summary>
        /// 事件回调
        /// </summary>
        public System.Action<long> OnRollback;
        public System.Action<long> OnPrediction;

        void Start()
        {
            // 初始化当前游戏状态
            currentGameState = ECSGameState.CreateSnapshot(world, 0);
        }

        /// <summary>
        /// 保存当前帧的状态快照
        /// </summary>
        public void SaveSnapshot(long frameNumber)
        {
            if (!enablePredictionRollback)
                return;

            // 从World创建快照
            var snapshot = ECSGameState.CreateSnapshot(world, frameNumber);
            snapshotHistory[frameNumber] = snapshot;

            // 清理旧的快照
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
                    fireInputHistory.Remove(frame);
                }
            }
        }

        /// <summary>
        /// 加载指定帧的状态快照
        /// </summary>
        public ECSGameState LoadSnapshot(long frameNumber)
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
            fireInputHistory[frameNumber] = new Dictionary<int, bool>();

            foreach (var frame in serverFrame.FrameDatas)
            {
                inputHistory[frameNumber][frame.PlayerId] = frame.Direction;
                // 这里暂时不处理发射输入，可以后续扩展ServerFrame添加fire字段
                fireInputHistory[frameNumber][frame.PlayerId] = false;
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

        /// <summary>
        /// 获取指定帧的发射输入
        /// </summary>
        public Dictionary<int, bool> GetFireInputs(long frameNumber)
        {
            if (fireInputHistory.ContainsKey(frameNumber))
            {
                return new Dictionary<int, bool>(fireInputHistory[frameNumber]);
            }
            return new Dictionary<int, bool>();
        }

        public long predictedFrameIndex = 1;

        /// <summary>
        /// 客户端预测：立即执行输入
        /// </summary>
        public void PredictInput(int playerId, InputDirection direction, bool fire = false)
        {
            if (!enablePredictionRollback)
                return;

            long frameNumber = confirmedServerFrame + predictedFrameIndex++;

            // 保存输入
            inputHistory[frameNumber] = new Dictionary<int, InputDirection> { { playerId, direction } };
            fireInputHistory[frameNumber] = new Dictionary<int, bool> { { playerId, fire } };

            // 执行预测
            var inputs = new Dictionary<int, InputDirection> { { playerId, direction } };
            var fireInputs = new Dictionary<int, bool> { { playerId, fire } };
            world = ECSStateMachine.Execute(world, inputs, fireInputs);

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
                return;

            long serverFrameNumber = serverFrame.FrameNumber;

            // 检查是否需要回滚
            bool needRollback = false;

            if (serverFrameNumber <= confirmedServerFrame)
            {
                // 重复包，忽略
                return;
            }

            if (predictedFrame > confirmedServerFrame)
            {
                // 检查输入是否一致
                var serverInputs = new Dictionary<int, InputDirection>();
                foreach (var frameData in serverFrame.FrameDatas)
                {
                    serverInputs[frameData.PlayerId] = frameData.Direction;
                }

                var predictedInputs = GetInputs(serverFrameNumber);
                if (serverInputs.Count != predictedInputs.Count)
                {
                    needRollback = true;
                }
                else
                {
                    foreach (var kvp in serverInputs)
                    {
                        if (!predictedInputs.ContainsKey(kvp.Key) || predictedInputs[kvp.Key] != kvp.Value)
                        {
                            needRollback = true;
                            break;
                        }
                    }
                }
            }

            if (needRollback)
            {
                // 回滚
                var snapshot = LoadSnapshot(confirmedServerFrame);
                if (snapshot != null)
                {
                    snapshot.RestoreToWorld(world);
                    ECSSyncHelper.SyncFromWorldToUnity(world);

                    // 重新执行从confirmedServerFrame到serverFrameNumber的所有帧
                    for (long frame = confirmedServerFrame + 1; frame <= serverFrameNumber; frame++)
                    {
                        var inputs = GetInputs(frame);
                        var fireInputs = GetFireInputs(frame);
                        world = ECSStateMachine.Execute(world, inputs, fireInputs);
                        SaveSnapshot(frame);
                    }

                    OnRollback?.Invoke(serverFrameNumber);
                }
            }
            else
            {
                // 不需要回滚，直接执行服务器帧
                SaveInput(serverFrameNumber, serverFrame);
                var inputs = GetInputs(serverFrameNumber);
                var fireInputs = GetFireInputs(serverFrameNumber);
                world = ECSStateMachine.Execute(world, inputs, fireInputs);
                SaveSnapshot(serverFrameNumber);
            }

            confirmedServerFrame = Math.Max(confirmedServerFrame, serverFrameNumber);
            predictedFrameIndex = 1;
        }

        /// <summary>
        /// 清理历史数据
        /// </summary>
        public void ClearHistory()
        {
            snapshotHistory.Clear();
            inputHistory.Clear();
            fireInputHistory.Clear();
            confirmedServerFrame = -1;
            predictedFrame = 0;
            world.Clear();
        }
    }
}

