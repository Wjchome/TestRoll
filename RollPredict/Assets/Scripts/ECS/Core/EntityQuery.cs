using System;
using System.Collections.Generic;
using System.Linq;

namespace Frame.ECS
{
    /// <summary>
    /// Entity查询：灵活的Entity过滤系统
    /// 
    /// 功能：
    /// - WithAll: 必须有所有指定的Component
    /// - WithAny: 至少有一个指定的Component
    /// - WithNone: 不能有任何指定的Component
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
    public class EntityQuery
    {
        private readonly World _world;
        
        // 查询条件
        private HashSet<Type> _allTypes = new HashSet<Type>();      // 必须有所有这些Component
        private HashSet<Type> _anyTypes = new HashSet<Type>();      // 至少有一个这些Component
        private HashSet<Type> _noneTypes = new HashSet<Type>();     // 不能有任何这些Component

        
        public EntityQuery(World world)
        {
            _world = world;
        }
        
        /// <summary>
        /// 必须有指定的Component（单个）
        /// </summary>
        public EntityQuery WithAll<T>() where T : IComponent
        {
            _allTypes.Add(typeof(T));
            return this;
        }
        
        /// <summary>
        /// 必须有所有指定的Component（2个）
        /// </summary>
        public EntityQuery WithAll<T1, T2>() 
            where T1 : IComponent 
            where T2 : IComponent
        {
            _allTypes.Add(typeof(T1));
            _allTypes.Add(typeof(T2));
            return this;
        }
        
        /// <summary>
        /// 必须有所有指定的Component（3个）
        /// </summary>
        public EntityQuery WithAll<T1, T2, T3>() 
            where T1 : IComponent 
            where T2 : IComponent 
            where T3 : IComponent
        {
            _allTypes.Add(typeof(T1));
            _allTypes.Add(typeof(T2));
            _allTypes.Add(typeof(T3));
            return this;
        }
        
        /// <summary>
        /// 至少有一个指定的Component（单个）
        /// </summary>
        public EntityQuery WithAny<T>() where T : IComponent
        {
            _anyTypes.Add(typeof(T));
            return this;
        }
        
        /// <summary>
        /// 至少有一个指定的Component（2个）
        /// </summary>
        public EntityQuery WithAny<T1, T2>() 
            where T1 : IComponent 
            where T2 : IComponent
        {
            _anyTypes.Add(typeof(T1));
            _anyTypes.Add(typeof(T2));
            return this;
        }
        
        /// <summary>
        /// 至少有一个指定的Component（3个）
        /// </summary>
        public EntityQuery WithAny<T1, T2, T3>() 
            where T1 : IComponent 
            where T2 : IComponent 
            where T3 : IComponent
        {
            _anyTypes.Add(typeof(T1));
            _anyTypes.Add(typeof(T2));
            _anyTypes.Add(typeof(T3));
            return this;
        }
        
        /// <summary>
        /// 不能有指定的Component（单个）
        /// </summary>
        public EntityQuery WithNone<T>() where T : IComponent
        {
            _noneTypes.Add(typeof(T));
            return this;
        }
        
        /// <summary>
        /// 不能有任何指定的Component（2个）
        /// </summary>
        public EntityQuery WithNone<T1, T2>() 
            where T1 : IComponent 
            where T2 : IComponent
        {
            _noneTypes.Add(typeof(T1));
            _noneTypes.Add(typeof(T2));
            return this;
        }
        
        /// <summary>
        /// 不能有任何指定的Component（3个）
        /// </summary>
        public EntityQuery WithNone<T1, T2, T3>() 
            where T1 : IComponent 
            where T2 : IComponent 
            where T3 : IComponent
        {
            _noneTypes.Add(typeof(T1));
            _noneTypes.Add(typeof(T2));
            _noneTypes.Add(typeof(T3));
            return this;
        }
        
        /// <summary>
        /// 获取符合条件的所有Entity
        /// </summary>
        public IEnumerable<Entity> GetEntities()
        {
            return ExecuteQuery();
        }
        
        /// <summary>
        /// 执行查询逻辑
        /// </summary>
        private List<Entity> ExecuteQuery()
        {
            var result = new List<Entity>();
            
            // 获取所有Entity
            foreach (var entity in _world.GetAllEntities())
            {
                // 检查是否满足所有条件
                if (MatchesQuery(entity))
                {
                    result.Add(entity);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 检查Entity是否匹配查询条件
        /// </summary>
        private bool MatchesQuery(Entity entity)
        {
            // 1. 检查 WithAll: 必须有所有指定的Component
            foreach (var type in _allTypes)
            {
                if (!HasComponent(entity, type))
                {
                    return false;  // 缺少必需的Component
                }
            }
            
            // 2. 检查 WithAny: 至少有一个指定的Component
            if (_anyTypes.Count > 0)
            {
                bool hasAny = false;
                foreach (var type in _anyTypes)
                {
                    if (HasComponent(entity, type))
                    {
                        hasAny = true;
                        break;
                    }
                }
                
                if (!hasAny)
                {
                    return false;  // 没有任何指定的Component
                }
            }
            
            // 3. 检查 WithNone: 不能有任何指定的Component
            foreach (var type in _noneTypes)
            {
                if (HasComponent(entity, type))
                {
                    return false;  // 有不该有的Component
                }
            }
            
            // 通过所有检查
            return true;
        }
        
        /// <summary>
        /// 检查Entity是否有指定类型的Component（无反射版本）
        /// </summary>
        private bool HasComponent(Entity entity, Type componentType)
        {
            // 使用World的HasComponentOfType方法（无反射）
            return _world.HasComponentOfType(entity, componentType);
        }
        
        /// <summary>
        /// 获取查询描述（用于调试）
        /// </summary>
        public override string ToString()
        {
            var parts = new List<string>();
            
            if (_allTypes.Count > 0)
            {
                parts.Add($"WithAll({string.Join(", ", _allTypes.Select(t => t.Name))})");
            }
            
            if (_anyTypes.Count > 0)
            {
                parts.Add($"WithAny({string.Join(", ", _anyTypes.Select(t => t.Name))})");
            }
            
            if (_noneTypes.Count > 0)
            {
                parts.Add($"WithNone({string.Join(", ", _noneTypes.Select(t => t.Name))})");
            }
            
            return $"EntityQuery[{string.Join(" + ", parts)}]";
        }
    }
}

