using System;
using Frame.ECS;
using Frame.FixMath;

namespace Frame.ECS
{
    /// <summary>
    /// 玩家Component：存储玩家特有的状态
    /// 
    /// 注意：
    /// - HP 和僵直状态已分离到通用组件（HPComponent, StiffComponent）
    /// - 此组件只包含玩家特有的数据
    /// </summary>
    [Serializable]
    public struct PlayerComponent : IComponent
    {
        /// <summary>
        /// 玩家ID（游戏逻辑层的ID，如100, 200, 300...）
        /// </summary>
        public int playerId;
        
        /// <summary>
        /// 当前模式索引（0=放置墙, 1=发射子弹）
        /// </summary>
        public int currentIndex;
        
        /// <summary>
        /// 总模式数量
        /// </summary>
        public int sumIndex;
        
        /// <summary>
        /// 子弹发射冷却时间（Fix64，确保确定性）
        /// </summary>
        public Fix64 bulletCooldownTimer;
        
        /// <summary>
        /// 放置墙冷却时间（Fix64，确保确定性）
        /// </summary>
        public Fix64 wallCooldownTimer;
        
        /// <summary>
        /// 子弹发射冷却时间配置（帧数）
        /// 例如：18 表示 18 帧的冷却（假设 60fps = 0.3 秒）
        /// </summary>
        public static Fix64 BulletCooldownDuration = (Fix64)1; // 18帧 ≈ 0.3秒 @ 60fps
        
        /// <summary>
        /// 放置墙冷却时间配置（帧数）
        /// 例如：30 表示 30 帧的冷却（假设 60fps = 0.5 秒）
        /// </summary>
        public static Fix64 WallCooldownDuration = (Fix64)1; // 30帧 ≈ 0.5秒 @ 60fps

        public PlayerComponent(int playerId, int sumIndex)
        {
            this.playerId = playerId;
            currentIndex = 0;
            this.sumIndex = sumIndex;
            this.bulletCooldownTimer = Fix64.Zero;
            this.wallCooldownTimer = Fix64.Zero;
        }

        public object Clone()
        {
            return this;
        }

        public override string ToString()
        {
            return $"{this.GetType().Name}: playerId={playerId}, currentIndex={currentIndex}";
        }
    
    }
}

