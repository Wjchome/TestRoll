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

        public PlayerComponent(int playerId, int hp)
        {
            this.playerId = playerId;
            this.hp = hp;
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

