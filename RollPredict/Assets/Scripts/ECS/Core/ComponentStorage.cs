using System;
using System.Collections.Generic;

namespace Frame.ECS
{
    /// <summary>
    /// Component存储：为每种Component类型提供独立的存储（紧密排列版本）
    /// 
    /// 设计：
    /// - 使用List<TComponent>存储组件（紧密排列，CPU缓存友好）
    /// - 使用Dictionary<Entity, int>维护Entity到数组索引的映射
    /// - 使用List<Entity>维护索引到Entity的映射（用于遍历）
    /// 
    /// 优势：
    /// 1. 紧密排列：组件数据连续存储，提高CPU缓存命中率
    /// 2. 批量处理高效：可以顺序遍历数组，性能更好
    /// 3. 复杂度不变：所有操作的时间复杂度与之前相同
    /// </summary>
    public class ComponentStorage<TComponent> : IComponentStorage where TComponent : struct, IComponent
    {
        //   
        // 紧密排列的组件数组
        private List<TComponent> _components = new List<TComponent>();

        // Entity -> 数组索引映射（O(1)查找）
        private Dictionary<Entity, int> _entityToIndex = new Dictionary<Entity, int>();

        // 数组索引 -> Entity映射（用于遍历和反向查找）
        private List<Entity> _indexToEntity = new List<Entity>();

        /// <summary>
        /// 添加或更新Component（O(1)）
        /// </summary>
        public void Set(Entity entity, TComponent component)
        {
            if (_entityToIndex.TryGetValue(entity, out int index))
            {
                // 更新现有组件（原地更新，保持紧密排列）
                _components[index] = component;
            }
            else
            {
                // 添加新组件（追加到数组末尾，保持紧密排列）
                int newIndex = _components.Count;
                _components.Add(component);
                _entityToIndex[entity] = newIndex;
                _indexToEntity.Add(entity);
            }
        }

        /// <summary>
        /// 获取Component（O(1)）
        /// </summary>
        public bool TryGet(Entity entity, out TComponent component)
        {
            if (_entityToIndex.TryGetValue(entity, out int index))
            {
                component = _components[index];
                return true;
            }

            component = default(TComponent);
            return false;
        }

        /// <summary>
        /// 获取Component（如果不存在返回默认值）（O(1)）
        /// </summary>
        public TComponent Get(Entity entity)
        {
            return _entityToIndex.TryGetValue(entity, out int index)
                ? _components[index]
                : default(TComponent);
        }

        /// <summary>
        /// 移除Component（O(1)，使用"交换并删除"技术保持紧密排列）
        /// </summary>
        public bool Remove(Entity entity)
        {
            if (!_entityToIndex.TryGetValue(entity, out int index))
                return false;

            int lastIndex = _components.Count - 1;

            if (index != lastIndex)
            {
                // 将最后一个元素移到当前位置（保持紧密排列）
                _components[index] = _components[lastIndex];
                Entity lastEntity = _indexToEntity[lastIndex];
                _entityToIndex[lastEntity] = index;
                _indexToEntity[index] = lastEntity;
            }

            // 删除最后一个元素
            _components.RemoveAt(lastIndex);
            _indexToEntity.RemoveAt(lastIndex);
            _entityToIndex.Remove(entity);

            return true;
        }

        /// <summary>
        /// 检查Entity是否有此Component（O(1)）
        /// </summary>
        public bool Has(Entity entity)
        {
            return _entityToIndex.ContainsKey(entity);
        }

        /// <summary>
        /// 获取所有Entity（O(n)遍历）
        /// </summary>
        public IEnumerable<Entity> GetAllEntities()
        {
            return _indexToEntity;
        }

        /// <summary>
        /// 获取所有Component（用于状态快照）（O(n)）
        /// 返回OrderedDictionary以保持API兼容性
        /// </summary>
        public OrderedDictionary<Entity, TComponent> GetAllComponents()
        {
            var result = new OrderedDictionary<Entity, TComponent>();
            for (int i = 0; i < _components.Count; i++)
            {
                result.Add(_indexToEntity[i], _components[i]);
            }

            return result;
        }

        /// <summary>
        /// 批量设置Component（用于状态恢复）（O(n)）
        /// </summary>
        public void SetAll(OrderedDictionary<Entity, TComponent> components)
        {
            Clear();
            foreach (var kvp in components)
            {
                Set(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// 清空所有Component（O(1)）
        /// </summary>
        public void Clear()
        {
            _components.Clear();
            _entityToIndex.Clear();
            _indexToEntity.Clear();
        }

        /// <summary>
        /// 获取Component数量（O(1)）
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
        /// 批量设置Component（接口实现，从IComponent类型恢复）
        /// </summary>
        void IComponentStorage.SetAllAsIComponent(OrderedDictionary<Entity, IComponent> components)
        {
            Clear();
            foreach (var kvp in components)
            {
                if (kvp.Value is TComponent typedComponent)
                {
                    Set(kvp.Key, typedComponent);
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

        /// <summary>
        /// 深拷贝ComponentStorage（接口实现）
        /// </summary>
        IComponentStorage IComponentStorage.Clone()
        {
            return Clone();
        }

        // 静态缓存：标记哪些组件类型需要深拷贝（包含引用类型字段）
        // 性能优化：避免每次克隆都进行类型检查，避免纯值类型组件的装箱/拆箱
        private static readonly HashSet<Type> _componentsNeedingDeepClone = new HashSet<Type>
        {
            typeof(GridMapComponent),      // 包含 OrderedHashSet<GridNode>
            typeof(FlowFieldComponent)      // 包含 List<FixVector2>
        };

        /// <summary>
        /// 深拷贝ComponentStorage（用于快照）
        /// 
        /// 关键：即使Component是struct（值类型），如果它包含引用类型字段（如List、OrderedHashSet等），
        /// 直接拷贝struct只会浅拷贝引用字段，导致多个组件共享同一个引用对象。
        /// 
        /// 解决方案：
        /// - 对于纯值类型组件：直接拷贝（避免装箱/拆箱，性能最优）
        /// - 对于包含引用类型的组件：调用Clone()方法进行深拷贝
        /// 
        /// 性能优化：
        /// - 使用静态缓存标记需要深拷贝的组件类型，避免反射检查
        /// - 纯值类型组件直接拷贝（O(1)，无装箱/拆箱开销）
        /// - 只有2个组件类型需要深拷贝：GridMapComponent、ZombieAIComponent
        /// </summary>
        public ComponentStorage<TComponent> Clone()
        {
            var cloned = new ComponentStorage<TComponent>();

            // 检查组件类型是否需要深拷贝
            Type componentType = typeof(TComponent);
            bool needsDeepClone = _componentsNeedingDeepClone.Contains(componentType);

            // 深拷贝所有Component
            cloned._components = new List<TComponent>(this._components.Count);
            
            if (needsDeepClone)
            {
                // 包含引用类型字段，必须调用Clone()进行深拷贝
                for (int i = 0; i < this._components.Count; i++)
                {
                    var component = this._components[i];
                    // 调用Clone()，会深拷贝引用类型字段（如OrderedHashSet、List）
                    cloned._components.Add((TComponent)component.Clone());
                }
            }
            else
            {
                // 纯值类型组件，直接拷贝（避免装箱/拆箱，性能最优）
                for (int i = 0; i < this._components.Count; i++)
                {
                    cloned._components.Add(this._components[i]);
                }
            }

            // 深拷贝映射字典
            cloned._entityToIndex = new Dictionary<Entity, int>(this._entityToIndex);
            cloned._indexToEntity = new List<Entity>(this._indexToEntity);

            return cloned;
        }
    }
}