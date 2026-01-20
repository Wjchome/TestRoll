using System;
using Frame.FixMath;

namespace Frame.ECS
{
    /// <summary>
    /// 爆炸组件：表示一个爆炸效果
    /// 
    /// 特性：
    /// - 爆炸是瞬时的（在创建的那一帧造成伤害）
    /// - 用于视觉效果的持续显示
    /// - 一定帧数后自动销毁
    /// 
    /// 使用场景：
    /// - 油桶爆炸
    /// - 手榴弹爆炸
    /// - 导弹爆炸
    /// </summary>
    [Serializable]
    public struct ExplosionComponent : IComponent
    {
        /// <summary>
        /// 爆炸中心位置
        /// </summary>
        public FixVector2 position;
        
        /// <summary>
        /// 爆炸范围（半径）
        /// </summary>
        public Fix64 radius;
        
        /// <summary>
        /// 爆炸伤害
        /// </summary>
        public int damage;
        
        /// <summary>
        /// 当前已存在的帧数
        /// </summary>
        public int currentFrame;


        public ExplosionComponent(FixVector2 position, Fix64 radius, int damage)
        {
            this.position = position;
            this.radius = radius;
            this.damage = damage;
            currentFrame = 0;
        }
       

        public object Clone()
        {
            return this;
        }

        public override string ToString()
        {
            return $"Explosion: Pos={position}, Radius={radius}, Damage={damage}";
        }
    }
}



