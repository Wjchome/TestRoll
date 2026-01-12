using System;
using System.Collections.Generic;
using System.Linq;

namespace Frame.ECS
{
    /// <summary>
    /// 碰撞信息组件：存储当前帧的碰撞信息
    /// 由PhysicsSystem每帧更新，供其他System查询使用
    /// </summary>
    [Serializable]
    public struct CollisionComponent : IComponent
    {
        /// <summary>
        /// 当前帧碰撞的Entity ID列表
        /// 每帧开始时由PhysicsSystem清空，然后填充当前帧的碰撞结果
        /// </summary>
        public List<int> collidingEntityIds;

        public CollisionComponent(List<int> collidingEntityIds = null)
        {
            this.collidingEntityIds = collidingEntityIds ?? new List<int>();
        }

        /// <summary>
        /// 添加碰撞的Entity ID
        /// </summary>
        public void AddCollidingEntity(int entityId)
        {
            if (collidingEntityIds == null)
                collidingEntityIds = new List<int>();
            
            if (!collidingEntityIds.Contains(entityId))
                collidingEntityIds.Add(entityId);
        }

        /// <summary>
        /// 清空碰撞信息
        /// </summary>
        public void Clear()
        {
            collidingEntityIds?.Clear();
        }

        public object Clone()
        {
            return new CollisionComponent(
                collidingEntityIds?.ToList() ?? new List<int>()
            );
        }

        public override string ToString()
        {
            if (collidingEntityIds == null || collidingEntityIds.Count == 0)
                return $"{GetType().Name}: No collisions";
            
            return $"{GetType().Name}: {collidingEntityIds.Count} collisions [{string.Join(", ", collidingEntityIds)}]";
        }
    }
}


