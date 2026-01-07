using System;

namespace Frame.ECS
{
    /// <summary>
    /// Entity：只是一个ID，不包含任何数据或逻辑
    /// 在ECS架构中，Entity是游戏对象的唯一标识符
    /// 
    /// 优势：
    /// 1. 完全解耦：Entity ID是稳定的，不依赖Unity对象生命周期
    /// 2. 确定性：ID是整数，可以直接序列化
    /// 3. 性能：查找和比较都是O(1)操作
    /// </summary>
    public struct Entity : IEquatable<Entity>
    {
        /// <summary>
        /// Entity的唯一标识符
        /// </summary>
        public readonly int Id;

        /// <summary>
        /// 无效的Entity（ID为0表示无效）
        /// </summary>
        public static readonly Entity Invalid = new Entity(0);

        public Entity(int id)
        {
            Id = id;
        }

        public bool IsValid => Id != 0;

        public bool Equals(Entity other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is Entity other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public static bool operator ==(Entity left, Entity right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Entity left, Entity right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"Entity({Id})";
        }
    }
}

