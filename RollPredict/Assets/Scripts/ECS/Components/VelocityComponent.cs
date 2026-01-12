using System;
using Frame.FixMath;

namespace Frame.ECS
{
    /// <summary>
    /// 速度组件：存储速度信息
    /// </summary>
    [Serializable]
    public struct VelocityComponent : IComponent
    {
        /// <summary>
        /// 速度（单位/秒）
        /// </summary>
        public FixVector2 velocity;

        public VelocityComponent(FixVector2 velocity)
        {
            this.velocity = velocity;
        }

        public object Clone()
        {
            return new VelocityComponent(velocity);
        }

        public override string ToString()
        {
            return $"{GetType().Name}: velocity = {velocity}";
        }
    }
}


