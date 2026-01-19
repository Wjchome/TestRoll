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
    /// 双World架构：confirmedWorld(服务器确认) + currentWorld(玩家看到)
    /// 相比传统的历史快照方式，更节省内存，逻辑更简单
    /// 适用于网络延迟不高、预测帧数不多的帧同步游戏
    /// </summary>
    public class ECSPredictionRollbackManager : SingletonMono<ECSPredictionRollbackManager>
    {
        [Tooltip("是否启用预测回滚")] public bool enablePredictionRollback = true;

        /// <summary>
        /// 确认的世界状态（服务器已确认的最新状态）
        /// </summary>
        public World confirmedWorld = new World();

        /// <summary>
        /// 当前使用的世界（玩家看到的预测状态）
        /// </summary>
        public World currentWorld = new World();

        /// <summary>
        /// 当前确认的服务器帧号
        /// </summary>
        public long confirmedServerFrame = 0;

        /// <summary>
        /// 当前预测的帧号
        /// </summary>
        public long predictedFrame = 0;

        /// <summary>
        /// 预测帧索引（相对于确认帧的偏移）
        /// </summary>
        public long predictedFrameIndex = 1;

        /// <summary>
        /// 客户端预测：立即执行输入
        /// </summary>
        public void PredictInput(int playerId, InputDirection direction, bool fire = false, long fireX = 0,
            long fireY = 0, bool isToggle = false)
        {
            if (!enablePredictionRollback)
                return;
            long frameNumber = confirmedServerFrame + predictedFrameIndex++;

            // 保存输入（只保存当前玩家的输入，其他玩家的输入会在收到服务器帧时补全）

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


            currentWorld = ECSStateMachine.Execute(currentWorld, new List<FrameData>
            {
                frameData
            });

            predictedFrame = frameNumber;
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
            else if (serverFrameNumber == confirmedServerFrame + 1)
            {
                // 更新确认世界 预测世界等于确认世界 当前世界等于
                confirmedWorld = ECSStateMachine.Execute(confirmedWorld, serverFrame.FrameDatas.ToList());
                currentWorld = confirmedWorld.Clone();

                predictedFrame = serverFrameNumber;
                confirmedServerFrame = serverFrameNumber;
                predictedFrameIndex = 1;
            }
            else
            {
                ECSFrameSyncExample.Instance.network.SendLossFrame(confirmedServerFrame);
            }
        }


        public void ProcessServerFrameNoPredict(ServerFrame serverFrame)
        {
            if (serverFrame.FrameNumber == confirmedServerFrame)
            {
                //重复包 无视
            }
            else if (serverFrame.FrameNumber == confirmedServerFrame + 1)
            {
                //对的
                currentWorld = ECSStateMachine.Execute(
                    currentWorld, serverFrame.FrameDatas.ToList());
                confirmedServerFrame = serverFrame.FrameNumber;
            }
            else
            {
                //包丢
                ECSFrameSyncExample.Instance.network.SendLossFrame(confirmedServerFrame);
            }
        }


        /// <summary>
        /// 清理历史数据
        /// </summary>
        public void ClearHistory()
        {
            confirmedServerFrame = 0;
            predictedFrame = 0;
            confirmedWorld.Clear();
            currentWorld = confirmedWorld;
        }
    }
}