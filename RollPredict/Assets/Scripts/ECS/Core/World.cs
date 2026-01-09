using System;
using System.Collections.Generic;
using Frame.ECS.Components;

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
        /// 恢复所有Component的状态（用于状态恢复）
        /// 接受 OrderedDictionary<Type, OrderedDictionary<Entity, IComponent>>
        /// 使用接口，避免反射
        /// </summary>
        public void RestoreComponentSnapshots(OrderedDictionary<Type, OrderedDictionary<Entity, IComponent>> snapshots)
        {
            foreach (var (type,components) in snapshots)
            {
                if (!_componentStorages.TryGetValue(type, out var storage))
                {
                    // 如果存储不存在，创建它（这里仍需要反射，但只执行一次）
                    var storageType = typeof(ComponentStorage<>).MakeGenericType(type);
                    storage = (IComponentStorage)Activator.CreateInstance(storageType);
                    _componentStorages[type] = storage;
                }

                // 使用接口方法，避免反射
                storage.SetAllAsIComponent(components);
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

        /// <summary>
        /// 获取Entity数量（用于调试）
        /// </summary>
        public int GetEntityCount()
        {
            return _entities.Count;
        }
    }
}

