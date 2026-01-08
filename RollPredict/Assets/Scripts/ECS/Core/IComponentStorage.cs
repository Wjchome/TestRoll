using System.Collections.Generic;

namespace Frame.ECS
{
    /// <summary>
    /// Component存储的接口：用于在World中统一管理不同类型的ComponentStorage
    /// 避免使用object，提供类型安全的访问方式
    /// </summary>
    public interface IComponentStorage
    {
        /// <summary>
        /// 移除指定Entity的Component
        /// </summary>
        bool Remove(Entity entity);

        /// <summary>
        /// 获取所有Component的快照（返回IComponent类型，用于序列化）
        /// </summary>
        OrderedDictionary<Entity, IComponent> GetAllComponentsAsIComponent();

        /// <summary>
        /// 批量设置Component（从IComponent类型恢复）
        /// </summary>
        void SetAllAsIComponent(OrderedDictionary<Entity, IComponent> components);

        /// <summary>
        /// 清空所有Component
        /// </summary>
        void Clear();

        /// <summary>
        /// 获取Component数量
        /// </summary>
        int Count { get; }
    }
}

