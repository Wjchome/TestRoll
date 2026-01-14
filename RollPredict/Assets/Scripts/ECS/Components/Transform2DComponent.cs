using System;
using Frame.FixMath;

namespace Frame.ECS
{
    /// <summary>
    /// 2D变换组件：存储位置信息
    /// </summary>
    [Serializable]
    public struct Transform2DComponent : IComponent
    {
        /// <summary>
        /// 位置（世界坐标）
        /// </summary>
        public FixVector2 position;

        public Transform2DComponent(FixVector2 position)
        {
            this.position = position;
        }

        public object Clone()
        {
            return this;
        }

        public override string ToString()
        {
            return $"{GetType().Name}: position = {position}";
        }
    }
}


