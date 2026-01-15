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
        /// 发射者Entity ID（哪个玩家发射的）
        /// </summary>
        public int ownerEntityId;
        
        /// <summary>
        /// 子弹伤害值
        /// </summary>
        public int damage;


        public BulletComponent(int ownerEntityId, int damage = 5)
        {
            this.ownerEntityId = ownerEntityId;
            this.damage = damage;
        }

        public object Clone()
        {
            return this;
        }
        public override string ToString()
        {
            return $"{GetType().Name}: ownerEntityId = {ownerEntityId}";
        }
    }
}

