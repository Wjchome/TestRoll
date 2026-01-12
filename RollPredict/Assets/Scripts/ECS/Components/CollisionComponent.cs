using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frame.ECS
{
    /// <summary>
    /// 碰撞信息组件：存储当前帧的碰撞信息
    /// 由PhysicsSystem每帧更新，供其他System查询使用
    /// 
    /// 设计说明：
    /// - 使用固定大小数组存储碰撞Entity ID，避免在结构体中使用List<>（引用类型）
    /// - 保持结构体的值语义，支持直接拷贝和快照/回滚
    /// - 无GC压力，性能优秀，适合帧同步
    /// - 参与快照和回滚，因为碰撞信息是游戏状态的一部分
    /// </summary>
    [Serializable]
    public struct CollisionComponent : IComponent
    {
        private const int MaxCollisions = 8;  // 最大碰撞数量

        private int _count;  // 当前碰撞数量
        
        // 使用多个字段存储碰撞Entity ID（固定数组）
        private int _c0, _c1, _c2, _c3, _c4, _c5, _c6, _c7;

        /// <summary>
        /// 当前碰撞数量
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// 添加碰撞的Entity ID
        /// </summary>
        public void AddCollidingEntity(int entityId)
        {
            // 检查是否已存在
            for (int i = 0; i < _count; i++)
            {
                if (GetCollision(i) == entityId)
                    return;
            }

            // 检查是否超出限制
            if (_count >= MaxCollisions)
            {
                // 超出限制，忽略（可以根据需要记录警告）
                Debug.LogWarning("碰撞物体大于"+MaxCollisions);
                return;
            }

            // 添加到数组
            SetCollision(_count++, entityId);
        }

        /// <summary>
        /// 获取指定索引的碰撞Entity ID
        /// </summary>
        public int GetCollision(int index)
        {
            if (index < 0 || index >= _count)
                return 0;

            return index switch
            {
                0 => _c0,
                1 => _c1,
                2 => _c2,
                3 => _c3,
                4 => _c4,
                5 => _c5,
                6 => _c6,
                7 => _c7,
                _ => 0
            };
        }

        /// <summary>
        /// 设置指定索引的碰撞Entity ID
        /// </summary>
        private void SetCollision(int index, int value)
        {
            switch (index)
            {
                case 0: _c0 = value; break;
                case 1: _c1 = value; break;
                case 2: _c2 = value; break;
                case 3: _c3 = value; break;
                case 4: _c4 = value; break;
                case 5: _c5 = value; break;
                case 6: _c6 = value; break;
                case 7: _c7 = value; break;
            }
        }

        /// <summary>
        /// 清空碰撞信息
        /// </summary>
        public void Clear()
        {
            _count = 0;
        }

        /// <summary>
        /// 检查是否包含指定的Entity ID
        /// </summary>
        public bool Contains(int entityId)
        {
            for (int i = 0; i < _count; i++)
            {
                if (GetCollision(i) == entityId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取所有碰撞的Entity ID（返回副本）
        /// </summary>
        public List<int> GetAllCollisions()
        {
            var result = new List<int>(_count);
            for (int i = 0; i < _count; i++)
            {
                result.Add(GetCollision(i));
            }
            return result;
        }

        public object Clone()
        {
            // 结构体直接拷贝即可（值类型）
            return this;
        }

        public override string ToString()
        {
            if (_count == 0)
                return $"{GetType().Name}: No collisions";

            var ids = new List<int>();
            for (int i = 0; i < _count; i++)
            {
                ids.Add(GetCollision(i));
            }
            return $"{GetType().Name}: {_count} collisions [{string.Join(", ", ids)}]";
        }
    }
}

