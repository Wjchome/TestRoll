using System;
using Frame.ECS;
using Frame.FixMath;

namespace Frame.ECS.Components
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
        /// 位置（世界坐标）
        /// </summary>
        public FixVector2 position;

        /// <summary>
        /// 生命值
        /// </summary>
        public int hp;

        public PlayerComponent(int playerId, FixVector2 position, int hp)
        {
            this.playerId = playerId;
            this.position = position;
            this.hp = hp;
        }

        public IComponent Clone()
        {
            return new PlayerComponent(playerId, position, hp);
        }
    }
}

