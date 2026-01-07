using System;
using Frame.ECS;
using Frame.FixMath;

namespace Frame.ECS.Components
{
    /// <summary>
    /// 子弹Component：存储子弹状态
    /// </summary>
    [Serializable]
    public struct BulletComponent : IComponent
    {
        /// <summary>
        /// 位置（世界坐标）
        /// </summary>
        public FixVector2 position;

        /// <summary>
        /// 速度
        /// </summary>
        public FixVector2 velocity;

        /// <summary>
        /// 发射者Entity ID（哪个玩家发射的）
        /// </summary>
        public int ownerEntityId;

        /// <summary>
        /// 子弹ID（用于标识子弹，可以用于碰撞检测等）
        /// </summary>
        public int bulletId;

        public BulletComponent(FixVector2 position, FixVector2 velocity, int ownerEntityId, int bulletId)
        {
            this.position = position;
            this.velocity = velocity;
            this.ownerEntityId = ownerEntityId;
            this.bulletId = bulletId;
        }

        public IComponent Clone()
        {
            return new BulletComponent(position, velocity, ownerEntityId, bulletId);
        }
    }
}

