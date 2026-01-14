using System;
using Frame.ECS;
using Frame.FixMath;

namespace Frame.ECS
{
    /// <summary>
    /// 玩家Component：存储玩家状态
    /// </summary>
    [Serializable]
    public struct PlayerComponent : IComponent
    {
        /// <summary>
        /// 玩家ID（游戏逻辑层的ID，如100, 200, 300...）
        /// </summary>
        public int playerId;
        

        /// <summary>
        /// 生命值
        /// </summary>
        public int hp;

        public int currentIndex;
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
        public static Fix64 BulletCooldownDuration = (Fix64)3; // 18帧 ≈ 0.3秒 @ 60fps
        
        /// <summary>
        /// 放置墙冷却时间配置（帧数）
        /// 例如：30 表示 30 帧的冷却（假设 60fps = 0.5 秒）
        /// </summary>
        public static Fix64 WallCooldownDuration = (Fix64)3; // 30帧 ≈ 0.5秒 @ 60fps

        public PlayerComponent(int playerId, int hp,  int sumIndex)
        {
            this.playerId = playerId;
            this.hp = hp;
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
            return $"{this.GetType().Name}:playerId = {playerId},hp = {hp}";
        }
    
    }
}

