using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DataStructure;
using Frame.Core;
using Frame.ECS;
using Google.Protobuf.Collections;
using Proto;
using UnityEngine;

namespace Frame.ECS
{
    /// <summary>
    /// ECS版本的预测回滚管理器
    /// 使用ECS World存储游戏状态
    /// </summary>
    public class ECSPredictionRollbackManager : SingletonMono<ECSPredictionRollbackManager>
    {
        [Header("配置")] [Tooltip("最大保存的快照数量")] public int maxSnapshots = 10;
        [Tooltip("最大保存的输入数量")] public int maxInputshots = 100;

        [Tooltip("是否启用预测回滚")] public bool enablePredictionRollback = true;

        [Header("关键帧优化")] [Tooltip("关键帧间隔（每N帧保存一次完整快照，0表示每帧都保存）")]
        public int keyframeInterval = 10;

        /// <summary>
        /// ECS World：存储所有游戏状态
        /// </summary>
        public World currentWorld = new World();

        /// <summary>
        /// 快照历史（按帧号索引）
        /// 使用World直接存储，避免转换开销
        /// </summary>
        private CircularBuffer<long, World> snapshotHistory;

        /// <summary>
        /// 输入历史（按帧号索引）
        /// </summary>
        private CircularBuffer<long, List<FrameData>> inputHistory;


        /// <summary>
        /// 当前确认的服务器帧号
        /// </summary>
        public long confirmedServerFrame = 0;

        /// <summary>
        /// 当前预测的帧号
        /// </summary>
        public long predictedFrame = 0;


        public bool enableLog;
        StringBuilder sb = new StringBuilder();
        StringBuilder sb1 = new StringBuilder();

        private void Start()
        {
            snapshotHistory = new CircularBuffer<long, World>(maxSnapshots);
            inputHistory = new CircularBuffer<long, List<FrameData>>(maxInputshots);
        }

        /// <summary>
        /// 保存当前帧的状态快照
        /// 
        /// 关键帧优化：
        /// - 如果keyframeInterval > 0：只在关键帧（每N帧）保存完整快照
        /// - 中间帧不保存快照，回滚时从最近的关键帧重新执行输入
        /// - 如果keyframeInterval == 0：每帧都保存（兼容旧行为）
        /// 
        /// 性能优化：
        /// - 内存占用：从 100帧*WorldSize 降低到 10帧*WorldSize（假设keyframeInterval=10）
        /// - 回滚开销：需要从关键帧重新执行，但通常只有几帧，开销可接受
        /// </summary>
        public void SaveSnapshot(long frameNumber)
        {
            if (!enablePredictionRollback)
                return;


            // 关键帧策略：只在关键帧保存完整快照
            bool isKeyframe = (frameNumber % keyframeInterval == 0);

            if (isKeyframe)
            {
                // 保存完整快照
                var snapshot = currentWorld.Clone();
                snapshotHistory[frameNumber] = snapshot;
            }
            // 中间帧不保存快照（节省内存）
            // 回滚时会从最近的关键帧重新执行输入
        }

        /// <summary>
        /// 检查指定帧是否是关键帧
        /// </summary>
        public bool IsKeyframe(long frameNumber)
        {
            return frameNumber % keyframeInterval == 0;
        }

        /// <summary>
        /// 获取指定帧之前最近的关键帧
        /// </summary>
        public long GetNearestKeyframe(long frameNumber)
        {
            // 向下取整到最近的关键帧
            return (frameNumber / keyframeInterval) * keyframeInterval;
        }

        /// <summary>
        /// 加载指定帧的状态快照
        /// 
        /// 关键帧优化：
        /// - 如果指定帧是关键帧，直接返回快照
        /// - 如果不是关键帧，从最近的关键帧重新执行输入
        /// 
        /// 性能：
        /// - 关键帧：O(1)直接返回
        /// - 非关键帧：O(k)重新执行k帧（k通常很小，1-9帧）
        /// </summary>
        public World LoadSnapshot(long frameNumber)
        {
            // 关键帧策略：从最近的关键帧重新执行
            long keyframe = GetNearestKeyframe(frameNumber);

            // 尝试获取关键帧快照
            if (!snapshotHistory.TryGetValue(keyframe, out var keyframeSnapshot))
            {
                Debug.LogWarning($"Keyframe snapshot for frame {keyframe} not found! (requested frame: {frameNumber})");
                return null;
            }

            // 如果就是关键帧，直接返回
            if (keyframe == frameNumber)
            {
                return keyframeSnapshot.Clone();
            }

            // 如果不是关键帧，从关键帧重新执行到目标帧
            // 这需要重新执行 (frameNumber - keyframe) 帧
            World world = keyframeSnapshot.Clone();

            // 从关键帧+1开始，执行到目标帧
            for (long frame = keyframe + 1; frame <= frameNumber; frame++)
            {
                var inputs = GetInputs(frame);
                world = ECSStateMachine.Execute(world, inputs);
            }

            return world;
        }

        /// <summary>
        /// 保存输入到历史记录
        /// </summary>
        public void SaveInput(long frameNumber, ServerFrame serverFrame)
        {
            if (!enablePredictionRollback)
                return;

            inputHistory[frameNumber] = serverFrame.FrameDatas.ToList();
        }

        /// <summary>
        /// 获取指定帧的输入
        /// </summary>
        public List<FrameData> GetInputs(long frameNumber)
        {
            if (inputHistory.ContainsKey(frameNumber))
            {
                return inputHistory[frameNumber].ToList();
            }

            return new List<FrameData>();
        }


        public long predictedFrameIndex = 1;

        /// <summary>
        /// 客户端预测：立即执行输入
        /// </summary>
        public void PredictInput(int playerId, InputDirection direction, bool fire = false, long fireX = 0,
            long fireY = 0,bool isToggle = false)
        {
            if (!enablePredictionRollback)
                return;
            long frameNumber = confirmedServerFrame + predictedFrameIndex++;

            // 保存输入（只保存当前玩家的输入，其他玩家的输入会在收到服务器帧时补全）
            bool isSave = direction != InputDirection.DirectionNone || fire||isToggle;

            if (isSave)
            {
                var frameData = new FrameData()
                {
                    PlayerId = playerId,
                    Direction = direction,
                    IsFire = fire,
                    IsToggle = isToggle,
                };

                // 如果发射，设置目标位置
                if (fire)
                {
                    frameData.FireX = fireX;
                    frameData.FireY = fireY;
                }

                inputHistory[frameNumber] = new List<FrameData>() { frameData };
            }
            else
            {
                inputHistory[frameNumber] = new List<FrameData>();
            }


            currentWorld = ECSStateMachine.Execute(currentWorld, inputHistory[frameNumber]);

            // 保存预测后的状态快照
            // 如果启用了关键帧优化，只在关键帧保存（或强制保存）
            bool shouldSave = true;
            if (keyframeInterval > 0)
            {
                // 只在关键帧保存（或强制保存）
                bool isKeyframe = IsKeyframe(frameNumber);
                shouldSave = isKeyframe;
            }

            if (shouldSave)
            {
                SaveSnapshot(frameNumber);
            }

            predictedFrame = frameNumber;
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

                    // 检查服务器帧中的输入是否与本地预测一致
                    if (serverFrame.FrameDatas.Count != GetInputs(serverFrameNumber).Count)
                    {
                        needRollback = true;
                    }
                    else
                    {
                        for (int i = 0; i < serverFrame.FrameDatas.Count; i++)
                        {
                            FrameData serverFrameData = serverFrame.FrameDatas[i];
                            var playerId = serverFrameData.PlayerId;
                            var direction = serverFrameData.Direction;
                            if (playerId != GetInputs(serverFrameNumber)[i].PlayerId)
                            {
                                needRollback = true;
                                break;
                            }
                            else if (direction != GetInputs(serverFrameNumber)[i].Direction)
                            {
                                needRollback = true;
                                break;
                            }
                            else if (serverFrameData.IsFire != GetInputs(serverFrameNumber)[i].IsFire)
                            {
                                needRollback = true;
                                break;
                            }
                            else if (serverFrameData.FireX != GetInputs(serverFrameNumber)[i].FireX)
                            {
                                needRollback = true;
                                break;
                            }
                            else if (serverFrameData.FireY != GetInputs(serverFrameNumber)[i].FireY)
                            {
                                needRollback = true;
                                break;
                            }
                            else if (serverFrameData.IsToggle != GetInputs(serverFrameNumber)[i].IsToggle)
                            {
                                needRollback = true;
                                break;
                            }
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
                case NetState.Repeat:
                    //暂时这样，丢包需要请求
                    break;
                case NetState.NoPredictionAndLose:
                case NetState.PredictAndLose:
                    FrameSyncNetwork.Instance.SendLossFrame(confirmedServerFrame);
                    break;
                case NetState.NoPredictionAndSuccess:

                    SaveInput(serverFrameNumber, serverFrame);

                    currentWorld = ECSStateMachine.Execute(currentWorld, serverFrame.FrameDatas.ToList());

                    // 保存服务器确认帧的快照（强制保存，因为这是确认帧）
                    SaveSnapshot(serverFrameNumber);

                    confirmedServerFrame = Math.Max(confirmedServerFrame, serverFrameNumber);
                    //predictedFrameIndex = Math.Max(1, predictedFrame - confirmedServerFrame + 1);
                    predictedFrameIndex = 1;
                    break;

                case NetState.PredictAndSuccessAndInputOk:

                    Debug.Log("PredictAndSuccessAndInputOk " + serverFrame);

                    if (predictedFrame != confirmedServerFrame + 1)
                    {
                        currentWorld.RestoreFrom(LoadSnapshot(serverFrameNumber));
                        
                    }
                    else
                    {
                        //可以跳过
                    }


                    predictedFrame = serverFrameNumber;
                    confirmedServerFrame = serverFrameNumber;
                    predictedFrameIndex = 1;
                    break;

                case NetState.PredictAndSuccessAndInputFail:
                    // 预测成功和失败的处理逻辑相同：
                    // 1. 都需要保存服务器输入（确保输入历史正确）
                    // 2. 都需要回滚到confirmedServerFrame（因为world可能已经执行到了更后面的帧）
                    // 3. 都需要用服务器确认的输入重新执行serverFrameNumber
                    // 4. 都需要保存快照并更新confirmedServerFrame
                    //
                    // 区别：
                    // - 预测成功：输入是正确的，但world状态可能已经偏离（因为执行了后续预测帧）
                    // - 预测失败：输入是错误的，world状态也是错误的
                    // 但处理方式相同：都回滚到confirmedServerFrame，然后用正确的输入重新执行


                    Debug.Log("PredictAndSuccessAndInputFail " + serverFrame);


                    // 保存服务器输入（必须在回滚前保存，确保GetInputs能获取到正确的输入）
                    SaveInput(serverFrameNumber, serverFrame);

                    // 回滚到confirmedServerFrame

                    currentWorld.RestoreFrom(LoadSnapshot(confirmedServerFrame));


                    // 用服务器确认的输入重新执行serverFrameNumber
                    currentWorld = ECSStateMachine.Execute(currentWorld, serverFrame.FrameDatas.ToList());

                    // 保存服务器确认帧的快照（强制保存，因为这是确认帧）
                    SaveSnapshot(serverFrameNumber);

                    predictedFrame = serverFrameNumber;
                    confirmedServerFrame = serverFrameNumber;
                    predictedFrameIndex = 1;
                    break;
            }

            sb.AppendLine(
                $"[Frame {serverFrameNumber}] {currentNetState} | ConfirmedFrame: {confirmedServerFrame} | PredictedFrame: {predictedFrame}");
        }

        private void OnDisable()
        {
            if (enableLog)
            {
                Debug.Log(sb1.ToString());
                if (sb.Length > 0)
                {
                    try
                    {
                        string filePath = Path.Combine(Application.dataPath,
                            $"prediction_rollback_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                        File.WriteAllText(filePath, sb.ToString());
                        Debug.Log($"Prediction rollback log saved to: {filePath}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to write prediction rollback log to file: {e.Message}");
                    }
                }
            }
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
            currentWorld.Clear();
        }
    }
}