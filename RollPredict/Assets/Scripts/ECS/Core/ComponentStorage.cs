using System;
using System.Collections.Generic;

namespace Frame.ECS
{


    /// <summary>
    /// Component存储：为每种Component类型提供独立的存储
    /// 
    /// 设计：
    /// - 使用Dictionary<Entity, TComponent>存储每个Entity的特定 Component
    /// - 支持快速查找和更新
    /// - 支持批量操作（用于状态快照）
    ///     其实目前来说是一个Entity对应一个IComponent
    ///     这个就是存储组件的，比如子弹有3个，entity id =  1，3，4
    ///     那么这里边就是 1 -> IComponent(1) 3 -> IComponent(3)...
    ///     如果有两个IComponent,一个玩家，一个子弹，那么这个应该只用实例化2个
    /// </summary>
    public class ComponentStorage<TComponent> : IComponentStorage where TComponent : IComponent
    {
        private OrderedDictionary<Entity, TComponent> _components = new OrderedDictionary<Entity, TComponent>();

        /// <summary>
        /// 添加或更新Component
        /// </summary>
        public void Set(Entity entity, TComponent component)
        {
            _components[entity] = component;
        }

        /// <summary>
        /// 获取Component
        /// </summary>
        public bool TryGet(Entity entity, out TComponent component)
        {
            return _components.TryGetValue(entity, out component);
        }

        /// <summary>
        /// 获取Component（如果不存在返回默认值）
        /// </summary>
        public TComponent Get(Entity entity)
        {
            return _components.TryGetValue(entity, out var component) ? component : default(TComponent);
        }

        /// <summary>
        /// 移除Component
        /// </summary>
        public bool Remove(Entity entity)
        {
            return _components.Remove(entity);
        }

        /// <summary>
        /// 检查Entity是否有此Component
        /// </summary>
        public bool Has(Entity entity)
        {
            return _components.ContainsKey(entity);
        }

        /// <summary>
        /// 获取所有Entity
        /// </summary>
        public IEnumerable<Entity> GetAllEntities()
        {
            return _components.Keys;
        }

        /// <summary>
        /// 获取所有Component（用于状态快照）
        /// </summary>
        public OrderedDictionary<Entity, TComponent> GetAllComponents()
        {
            return new OrderedDictionary<Entity, TComponent>(_components);
        }

        /// <summary>
        /// 批量设置Component（用于状态恢复）
        /// </summary>
        public void SetAll(OrderedDictionary<Entity, TComponent> components)
        {
            _components = new OrderedDictionary<Entity, TComponent>(components);
        }

        /// <summary>
        /// 清空所有Component
        /// </summary>
        public void Clear()
        {
            _components.Clear();
        }

        /// <summary>
        /// 获取Component数量
        /// </summary>
        public int Count => _components.Count;

        // IComponentStorage 接口实现

        /// <summary>
        /// 移除指定Entity的Component（接口实现）
        /// </summary>
        bool IComponentStorage.Remove(Entity entity)
        {
            return Remove(entity);
        }

        /// <summary>
        /// 获取所有Component的快照（接口实现，返回IComponent类型）
        /// </summary>
        OrderedDictionary<Entity, IComponent> IComponentStorage.GetAllComponentsAsIComponent()
        {
            var result = new OrderedDictionary<Entity, IComponent>();
            foreach (var kvp in _components)
            {
                if (kvp.Value is ICloneable cloneable)
                {
                    result.Add(kvp.Key, (IComponent)cloneable.Clone());
                }
                else
                {
                    // 对于值类型 （浅拷贝）或引用类型 （非常不建议）
                    result.Add(kvp.Key, kvp.Value);
                }
            }
            return  result;
        }

        /// <summary>
        /// 批量设置Component（接口实现，从IComponent类型恢复）
        /// </summary>
        void IComponentStorage.SetAllAsIComponent(OrderedDictionary<Entity, IComponent> components)
        {
            _components.Clear();
            foreach (var kvp in components)
            {
                if (kvp.Value is TComponent typedComponent)
                {
                    _components[kvp.Key] = typedComponent;
                }
            }
        }

        /// <summary>
        /// 清空所有Component（接口实现）
        /// </summary>
        void IComponentStorage.Clear()
        {
            Clear();
        }
    }
}

