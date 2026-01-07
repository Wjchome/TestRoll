using System;
using System.Collections.Generic;

namespace Frame.ECS
{
    /// <summary>
    /// ECS World：管理所有Entity和Component
    /// 
    /// 设计：
    /// - 使用Entity ID生成器分配唯一ID
    /// - 为每种Component类型维护独立的存储
    /// - 提供统一的Entity生命周期管理
    /// 
    /// 在预测回滚中：
    /// - World的状态就是所有Component的状态
    /// - 状态快照 = 所有ComponentStorage的快照
    /// - 回滚 = 恢复所有ComponentStorage的状态
    /// </summary>
    public class World
    {
        private int _nextEntityId = 1;
        private HashSet<Entity> _entities = new HashSet<Entity>();
        
        // Component存储：每种Component类型一个存储
        private OrderedDictionary<Type, object> _componentStorages = new OrderedDictionary<Type, object>();

        /// <summary>
        /// 创建新Entity
        /// </summary>
        public Entity CreateEntity()
        {
            var entity = new Entity(_nextEntityId++);
            _entities.Add(entity);
            return entity;
        }

        /// <summary>
        /// 销毁Entity（移除所有Component）
        /// </summary>
        public void DestroyEntity(Entity entity)
        {
            if (!_entities.Contains(entity))
                return;

            _entities.Remove(entity);

            // 移除该Entity的所有Component
            foreach (var (_,storage) in _componentStorages)
            {
                // 使用反射调用Remove方法（简化实现）
                var removeMethod = storage.GetType().GetMethod("Remove");
                removeMethod?.Invoke(storage, new object[] { entity });
            }
        }

        /// <summary>
        /// 检查Entity是否存在
        /// </summary>
        public bool HasEntity(Entity entity)
        {
            return _entities.Contains(entity);
        }

        /// <summary>
        /// 获取Component存储（如果不存在则创建）
        /// </summary>
        private ComponentStorage<TComponent> GetOrCreateStorage<TComponent>() where TComponent : IComponent
        {
            var type = typeof(TComponent);
            if (!_componentStorages.TryGetValue(type, out var storage))
            {
                storage = new ComponentStorage<TComponent>();
                _componentStorages[type] = storage;
            }
            return (ComponentStorage<TComponent>)storage;
        }

        /// <summary>
        /// 添加Component
        /// </summary>
        public void AddComponent<TComponent>(Entity entity, TComponent component) where TComponent : IComponent
        {
            var storage = GetOrCreateStorage<TComponent>();
            storage.Set(entity, component);
        }

        /// <summary>
        /// 获取Component
        /// </summary>
        public bool TryGetComponent<TComponent>(Entity entity, out TComponent component) where TComponent : IComponent
        {
            var type = typeof(TComponent);
            if (_componentStorages.TryGetValue(type, out var storage))
            {
                var typedStorage = (ComponentStorage<TComponent>)storage;
                return typedStorage.TryGet(entity, out component);
            }
            component = default(TComponent);
            return false;
        }

        /// <summary>
        /// 获取Component（如果不存在返回默认值）
        /// </summary>
        public TComponent GetComponent<TComponent>(Entity entity) where TComponent : IComponent
        {
            if (TryGetComponent(entity, out TComponent component))
            {
                return component;
            }
            return default(TComponent);
        }

        /// <summary>
        /// 移除Component
        /// </summary>
        public bool RemoveComponent<TComponent>(Entity entity) where TComponent : IComponent
        {
            var type = typeof(TComponent);
            if (_componentStorages.TryGetValue(type, out var storage))
            {
                var typedStorage = (ComponentStorage<TComponent>)storage;
                return typedStorage.Remove(entity);
            }
            return false;
        }

        /// <summary>
        /// 检查Entity是否有Component
        /// </summary>
        public bool HasComponent<TComponent>(Entity entity) where TComponent : IComponent
        {
            var type = typeof(TComponent);
            if (_componentStorages.TryGetValue(type, out var storage))
            {
                var typedStorage = (ComponentStorage<TComponent>)storage;
                return typedStorage.Has(entity);
            }
            return false;
        }

        /// <summary>
        /// 获取所有有指定Component的Entity（用于System遍历）
        /// </summary>
        public IEnumerable<Entity> GetEntitiesWithComponent<TComponent>() where TComponent : IComponent
        {
            var type = typeof(TComponent);
            if (_componentStorages.TryGetValue(type, out var storage))
            {
                var typedStorage = (ComponentStorage<TComponent>)storage;
                return typedStorage.GetAllEntities();
            }
            return new List<Entity>();
        }

        /// <summary>
        /// 获取所有Component的快照（用于状态保存）OrderedDictionary<Type,OrderedDictionary<Entity, TComponent> >
        /// </summary>
        public OrderedDictionary<Type, object> GetAllComponentSnapshots()
        {
            var snapshots = new OrderedDictionary<Type, object>();
            foreach (var (type,storage) in _componentStorages)
            {
                // 使用反射调用GetAllComponents方法
                var getAllMethod = storage.GetType().GetMethod("GetAllComponents");
                var snapshot = getAllMethod?.Invoke(storage, null);
                snapshots[type] = snapshot;
            }
            return snapshots;
        }

        /// <summary>
        /// 恢复所有Component的状态（用于状态恢复）
        /// </summary>
        public void RestoreComponentSnapshots(OrderedDictionary<Type, object> snapshots)
        {
            foreach (var (type,components) in snapshots)
            {
                if (!_componentStorages.TryGetValue(type, out var storage))
                {   // storage type ->  OrderedDictionary<Entity, TComponent> 
                    // 如果存储不存在，创建它
                    var storageType = typeof(ComponentStorage<>).MakeGenericType(type);
                    storage = Activator.CreateInstance(storageType);
                    _componentStorages[type] = storage;
                }

                // 使用反射调用SetAll方法
                var setAllMethod = storage.GetType().GetMethod("SetAll");
                setAllMethod?.Invoke(storage, new[] { components });
            }
        }

        /// <summary>
        /// 清空所有Entity和Component
        /// </summary>
        public void Clear()
        {
            _entities.Clear();
            _componentStorages.Clear();
            _nextEntityId = 1;
        }
    }
}

