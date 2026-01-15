using System;

namespace Frame.ECS
{
    /// <summary>
    /// 墙放置组件：标记墙正在放置中（等待放置者离开）
    /// 
    /// 使用场景：
    /// - 墙刚放置时，添加此组件，设置 isTrigger = true
    /// - 当放置者离开墙的范围后，移除此组件，设置 isTrigger = false
    /// - 这样墙在放置时不会阻挡放置者
    /// </summary>
    [Serializable]
    public struct WallPlacementComponent : IComponent
    {
        /// <summary>
        /// 放置者的Entity ID（谁放置的这面墙）
        /// </summary>
        public int placerEntityId;

        public WallPlacementComponent(int placerEntityId)
        {
            this.placerEntityId = placerEntityId;
        }

        public object Clone()
        {
            return this;
        }

        public override string ToString()
        {
            return $"{GetType().Name}: placer={placerEntityId}";
        }
    }
}

