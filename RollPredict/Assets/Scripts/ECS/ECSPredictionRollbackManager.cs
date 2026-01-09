using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        [Header("配置")] [Tooltip("最大保存的快照数量")] public int maxSnapshots = 100;

        [Tooltip("是否启用预测回滚")] public bool enablePredictionRollback = true;

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
        private Dictionary<long, List<FrameData>> inputHistory =
            new Dictionary<long, List<FrameData>>();


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

        public bool enableLog;
        StringBuilder sb = new StringBuilder();
        StringBuilder sb1 = new StringBuilder();
        /// <summary>
        /// 保存当前帧的状态快照   World + frameNumber    ->  ECSGameState
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
                }
            }
        }

        /// <summary>
        /// 加载指定帧的状态快照     frameNumber ->  ECSGameState
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
        public void PredictInput(int playerId, InputDirection direction, bool fire = false, long fireX = 0, long fireY = 0)
        {
            if (!enablePredictionRollback)
                return;

            long frameNumber = confirmedServerFrame + predictedFrameIndex++;

            // 保存输入（只保存当前玩家的输入，其他玩家的输入会在收到服务器帧时补全）
            var frameData = new FrameData()
            {
                PlayerId = playerId,
                Direction = direction,
                IsFire = fire
            };
            
            // 如果发射，设置目标位置
            if (fire)
            {
                frameData.FireX = fireX;
                frameData.FireY = fireY;
            }
            
            inputHistory[frameNumber] = new List<FrameData>() { frameData };

            world = ECSStateMachine.Execute(world, inputHistory[frameNumber]);

            // 保存预测后的状态快照
            SaveSnapshot(frameNumber);

            predictedFrame = Math.Max(predictedFrame, frameNumber);
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

                    world = ECSStateMachine.Execute(world, serverFrame.FrameDatas.ToList());
                    SaveSnapshot(serverFrameNumber);
                    confirmedServerFrame = Math.Max(confirmedServerFrame, serverFrameNumber);
                    //predictedFrameIndex = Math.Max(1, predictedFrame - confirmedServerFrame + 1);
                    predictedFrameIndex = 1;
                    break;

                case NetState.PredictAndSuccessAndInputOk:
                    Debug.Log("PredictAndSuccessAndInputOk " + serverFrame);
                    
                    currentGameState = LoadSnapshot(confirmedServerFrame);
                        //为什么这个地方需要加载
                    //如果预测多帧，那么现在会，比如预测到了4帧，然后2-4都是预测的，world也不对，现在已经到了第三帧，第三帧已经纠正过来了，world和预测时的world并不同
                    //检测点1 world
                   // sb1.AppendLine(serverFrameNumber.ToString()+" " + ECSGameState.CreateSnapshot(world, -1).ToString());
                    
                    currentGameState.RestoreToWorld(world);
                    world = ECSStateMachine.Execute(world, serverFrame.FrameDatas.ToList());
                    //检测点2 world
                    //sb1.AppendLine( ECSGameState.CreateSnapshot(world, -1).ToString());
                    //sb1.AppendLine();
                    SaveSnapshot(confirmedServerFrame + 1);
                    
                    confirmedServerFrame = Math.Max(confirmedServerFrame, serverFrameNumber);
                    //predictedFrameIndex = Math.Max(1, predictedFrame - confirmedServerFrame + 1);
                    predictedFrameIndex = 1;
                    

                    break;

                case NetState.PredictAndSuccessAndInputFail:
                    Debug.Log("PredictAndSuccessAndInputFail " + serverFrame);

                    // 先保存服务器输入（必须在回滚前保存，确保GetInputs能获取到正确的输入）
                    SaveInput(serverFrameNumber, serverFrame);


                    currentGameState = LoadSnapshot(confirmedServerFrame);

                    currentGameState.RestoreToWorld(world);

                    // 重新执行从 rollbackToFrame+1 到 serverFrameNumber 的所有帧
                    // 使用服务器的输入（已经在上面保存了）
                    for (long frame = confirmedServerFrame + 1; frame <= predictedFrame; frame++)
                    {
                        var newInputs = GetInputs(frame);

                        world = ECSStateMachine.Execute(world, newInputs);
                        SaveSnapshot(frame);
                    }

                    confirmedServerFrame = Math.Max(confirmedServerFrame, serverFrameNumber);
                    //predictedFrameIndex = Math.Max(1, predictedFrame - confirmedServerFrame + 1);
                    predictedFrameIndex = 1;
                    break;
            }

            sb.AppendLine(
                $"[Frame {serverFrameNumber}] {currentNetState} | ConfirmedFrame: {confirmedServerFrame} | PredictedFrame: {predictedFrame}");
            var confirmedSnapshot = LoadSnapshot(confirmedServerFrame);
            if (confirmedSnapshot != null)
            {
                sb.AppendLine($"ConfirmedState: {confirmedSnapshot}");
            }
            else
            {
                sb.AppendLine($"ConfirmedState: NULL (frame {confirmedServerFrame})");
            }
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
            world.Clear();
        }
    }
}