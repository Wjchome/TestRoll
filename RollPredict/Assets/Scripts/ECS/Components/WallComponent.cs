using System;

namespace Frame.ECS
{
    /// <summary>
    /// 墙组件：标记实体为墙
    /// </summary>
    [Serializable]
    public struct WallComponent : IComponent
    {
        public object Clone()
        {
            return this;
        }

        public override string ToString()
        {
            return $"{GetType().Name}";
        }
    }
}

