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
        // ⚠️ 帧同步关键：使用List而不是HashSet，确保遍历顺序确定性
        private OrderedHashSet<Entity> _entities = new OrderedHashSet<Entity>();
        
        // Component存储：每种Component类型一个存储
        // 使用IComponentStorage接口，类型更清晰，避免使用object
        private OrderedDictionary<Type, IComponentStorage> _componentStorages = new OrderedDictionary<Type, IComponentStorage>();

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

            // 移除该Entity的所有Component（使用接口，避免反射）
            foreach (var (_,storage) in _componentStorages)
            {
                storage.Remove(entity);
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
        /// 获取下一个Entity ID（用于快照）
        /// </summary>
        public int GetNextEntityId()
        {
            return _nextEntityId;
        }

        /// <summary>
        /// 获取所有Entity的列表（用于快照）
        /// 返回副本，保持顺序
        /// </summary>
        public OrderedHashSet<Entity> GetAllEntities()
        {
            return new OrderedHashSet<Entity>(_entities);
        }

        /// <summary>
        /// 恢复World元数据（用于回滚）
        /// </summary>
        public void RestoreMetadata(int nextEntityId, OrderedHashSet<Entity> entities)
        {
            _nextEntityId = nextEntityId;
            _entities = new OrderedHashSet<Entity>(entities);
        }

        /// <summary>
        /// 获取Component存储（如果不存在则创建）
        /// </summary>
        private ComponentStorage<TComponent> GetOrCreateStorage<TComponent>() where TComponent : struct,IComponent
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
        public void AddComponent<TComponent>(Entity entity, TComponent component) where TComponent : struct,IComponent
        {
            var storage = GetOrCreateStorage<TComponent>();
            storage.Set(entity, component);
        }

        /// <summary>
        /// 获取Component
        /// </summary>
        public bool TryGetComponent<TComponent>(Entity entity, out TComponent component) where TComponent :struct, IComponent
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
        public TComponent GetComponent<TComponent>(Entity entity) where TComponent : struct,IComponent
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
        public bool RemoveComponent<TComponent>(Entity entity) where TComponent : struct,IComponent
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
        public bool HasComponent<TComponent>(Entity entity) where TComponent :struct, IComponent
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
        /// 检查Entity是否有指定类型的Component（非泛型版本，用于查询系统）
        /// </summary>
        /// <param name="entity">要检查的Entity</param>
        /// <param name="componentType">Component类型</param>
        /// <returns>是否有该Component</returns>
        public bool HasComponentOfType(Entity entity, Type componentType)
        {
            if (_componentStorages.TryGetValue(componentType, out var storage))
            {
                // 使用接口方法检查（无反射）
                return storage.Has(entity);
            }
            return false;
        }

        /// <summary>
        /// 获取所有有指定Component的Entity（用于System遍历）
        /// </summary>
        public IEnumerable<Entity> GetEntitiesWithComponent<TComponent>() where TComponent : struct,IComponent
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
        /// 获取组件存储（内部方法，用于查询优化）
        /// </summary>
        internal IComponentStorage GetComponentStorage(Type componentType)
        {
            _componentStorages.TryGetValue(componentType, out var storage);
            return storage;
        }
        
        /// <summary>
        /// 批量获取组件（返回 Entity 和所有组件的元组）
        /// 用于查询有多个组件的 Entity 时，避免逐个 TryGet
        /// 
        /// 示例：
        /// foreach (var (entity, transform, velocity) in world.GetEntitiesWithComponents<Transform2DComponent, VelocityComponent>())
        /// {
        ///     // 直接使用 transform 和 velocity，无需 TryGet
        /// }
        /// </summary>
        public IEnumerable<(Entity entity, T1 component1)> GetEntitiesWithComponents<T1>() 
            where T1 : struct,IComponent
        {
            var storage1 = GetOrCreateStorage<T1>();
            foreach (var entity in storage1.GetAllEntities())
            {
                if (storage1.TryGet(entity, out var comp1))
                {
                    yield return (entity, comp1);
                }
            }
        }
        
        /// <summary>
        /// 批量获取组件（2个组件）
        /// </summary>
        public IEnumerable<(Entity entity, T1 component1, T2 component2)> GetEntitiesWithComponents<T1, T2>() 
            where T1 : struct,IComponent 
            where T2 :struct, IComponent
        {
            ComponentStorage<T1> storage1 = GetOrCreateStorage<T1>();
            ComponentStorage<T2>  storage2 = GetOrCreateStorage<T2>();
            
            // 从较小的集合开始遍历（优化）
            IComponentStorage smallerStorage = storage1.Count <= storage2.Count ? storage1 : storage2;
            
            foreach (var entity in smallerStorage.GetAllEntities())
            {
                if (storage1.TryGet(entity, out var comp1) && storage2.TryGet(entity, out var comp2))
                {
                    yield return (entity, comp1, comp2);
                }
            }
        }
        
        /// <summary>
        /// 批量获取组件（3个组件）
        /// </summary>
        public IEnumerable<(Entity entity, T1 component1, T2 component2, T3 component3)> GetEntitiesWithComponents<T1, T2, T3>() 
            where T1 : struct, IComponent 
            where T2 : struct,IComponent 
            where T3 : struct,IComponent
        {
            ComponentStorage<T1>  storage1 = GetOrCreateStorage<T1>();
            ComponentStorage<T2>  storage2 = GetOrCreateStorage<T2>();
            ComponentStorage<T3>  storage3 = GetOrCreateStorage<T3>();
            
            // 找到最小的集合（优化）
            (IComponentStorage,int)[] storages = new[] { ((IComponentStorage)storage1, 1), (storage2, 2), (storage3, 3) };
            var smallest = storages[0];
            foreach (var storage in storages)
            {
                if (storage.Item1.Count < smallest.Item1.Count)
                {
                    smallest = storage;
                }
            }
            
            // 从最小的集合开始遍历
            foreach (var entity in smallest.Item1.GetAllEntities())
            {
                if (storage1.TryGet(entity, out var comp1) && 
                    storage2.TryGet(entity, out var comp2) && 
                    storage3.TryGet(entity, out var comp3))
                {
                    yield return (entity, comp1, comp2, comp3);
                }
            }
        }
        
        /// <summary>
        /// 批量获取组件（4个组件）
        /// </summary>
        public IEnumerable<(Entity entity, T1 component1, T2 component2, T3 component3, T4 component4)> GetEntitiesWithComponents<T1, T2, T3, T4>() 
            where T1 : struct,IComponent 
            where T2 : struct,IComponent 
            where T3 :struct, IComponent
            where T4 : struct,IComponent
        {
            var storage1 = GetOrCreateStorage<T1>();
            var storage2 = GetOrCreateStorage<T2>();
            var storage3 = GetOrCreateStorage<T3>();
            var storage4 = GetOrCreateStorage<T4>();
            
            // 找到最小的集合（优化）
            var storages = new[] { 
                ((IComponentStorage)storage1, 1), (storage2, 2), (storage3, 3), (storage4, 4) 
            };
            var smallest = storages[0];
            foreach (var storage in storages)
            {
                if (storage.Item1.Count < smallest.Item1.Count)
                {
                    smallest = storage;
                }
            }
            
            // 从最小的集合开始遍历
            foreach (var entity in smallest.Item1.GetAllEntities())
            {
                if (storage1.TryGet(entity, out var comp1) && 
                    storage2.TryGet(entity, out var comp2) && 
                    storage3.TryGet(entity, out var comp3) &&
                    storage4.TryGet(entity, out var comp4))
                {
                    yield return (entity, comp1, comp2, comp3, comp4);
                }
            }
        }

        /// <summary>
        /// 获取所有Component的快照（用于状态保存）
        /// 返回 OrderedDictionary<Type, OrderedDictionary<Entity, IComponent>>
        /// 使用接口，避免反射
        /// </summary>
        public OrderedDictionary<Type, OrderedDictionary<Entity, IComponent>> GetAllComponentSnapshots()
        {
            var snapshots = new OrderedDictionary<Type, OrderedDictionary<Entity, IComponent>>();
            foreach (var (type,storage) in _componentStorages)
            {
                // 使用接口方法，避免反射
                var snapshot = storage.GetAllComponentsAsIComponent();
                snapshots[type] = snapshot;
            }
            return snapshots;
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

        /// <summary>
        /// 获取Entity数量（用于调试）
        /// </summary>
        public int GetEntityCount()
        {
            return _entities.Count;
        }

        /// <summary>
        /// 创建Entity查询
        /// 
        /// 示例：
        /// var query = world.CreateQuery()
        ///     .WithAll&lt;PlayerComponent, HealthComponent&gt;()
        ///     .WithNone&lt;DeadTag&gt;();
        /// 
        /// foreach (var entity in query.GetEntities())
        /// {
        ///     // 处理活着的玩家
        /// }
        /// </summary>
        /// <returns>新的Entity查询对象</returns>
        // public EntityQuery CreateQuery()
        // {
        //     return new EntityQuery(this);
        // }
    }
}

