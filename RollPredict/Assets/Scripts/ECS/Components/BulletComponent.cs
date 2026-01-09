using System;
using Frame.ECS;
using Frame.FixMath;

namespace Frame.ECS
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
        

        public BulletComponent(FixVector2 position, FixVector2 velocity, int ownerEntityId)
        {
            this.position = position;
            this.velocity = velocity;
            this.ownerEntityId = ownerEntityId;
        }

        public object Clone()
        {
            return new BulletComponent(position, velocity, ownerEntityId);
        }
        public override string ToString()
        {
            return $"{GetType().Name}: position = {position},velocity = {velocity},ownerEntityId = {ownerEntityId}";
        }
    }
}

