using System;
using Frame.FixMath;

namespace Frame.ECS
{
    /// <summary>
    /// 物理体组件：存储物理属性
    /// </summary>
    [Serializable]
    public struct PhysicsBodyComponent : IComponent
    {
        /// <summary>
        /// 质量（0或负数表示静态物体）
        /// </summary>
        public Fix64 mass;

        /// <summary>
        /// 是否为静态物体
        /// </summary>
        public bool isStatic;

        /// <summary>
        /// 是否受重力影响
        /// </summary>
        public bool useGravity;

        /// <summary>
        /// 是否为触发器（触发器不进行物理响应，只记录碰撞）
        /// </summary>
        public bool isTrigger;

        /// <summary>
        /// 弹性系数（0-1，0表示完全非弹性，1表示完全弹性）
        /// </summary>
        public Fix64 restitution;

        /// <summary>
        /// 摩擦系数（0-1）
        /// </summary>
        public Fix64 friction;

        /// <summary>
        /// 线性阻尼（0-1，用于在空地上减速）
        /// </summary>
        public Fix64 linearDamping;

        /// <summary>
        /// 物理层（用于碰撞过滤）
        /// </summary>
        public int layer;

        /// <summary>
        /// 是否为动态物体
        /// </summary>
        public bool IsDynamic => !isStatic && mass > Fix64.Zero;

        public PhysicsBodyComponent(
            Fix64 mass,
            bool isStatic = false,
            bool useGravity = true,
            bool isTrigger = false,
            Fix64 restitution = default,
            Fix64 friction = default,
            Fix64 linearDamping = default,
            int layer = 0)
        {
            this.mass = mass;
            this.isStatic = isStatic;
            this.useGravity = useGravity;
            this.isTrigger = isTrigger;
            this.restitution = restitution;
            this.friction = friction;
            this.linearDamping = linearDamping;
            this.layer = layer;
        }

        public object Clone()
        {
            return new PhysicsBodyComponent(
                mass,
                isStatic,
                useGravity,
                isTrigger,
                restitution,
                friction,
                linearDamping,
                layer
            );
        }

        public override string ToString()
        {
            return $"{GetType().Name}: mass={mass}, isStatic={isStatic}, layer={layer}";
        }
    }
}


