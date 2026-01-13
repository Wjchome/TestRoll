using System;

namespace Frame.ECS
{
    /// <summary>
    /// 网格节点：表示地图上的一个网格单元
    /// </summary>
    public struct GridNode : IEquatable<GridNode>
    {
        public int x;
        public int y;
        
        public GridNode(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
        
        public bool Equals(GridNode other)
        {
            return x == other.x && y == other.y;
        }
        
        public override bool Equals(object obj)
        {
            return obj is GridNode other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            // 假设 y < 10000，这样可以确保唯一性
            return x * 10000 + y;
        }
        
        public static bool operator ==(GridNode left, GridNode right)
        {
            return left.Equals(right);
        }
        
        public static bool operator !=(GridNode left, GridNode right)
        {
            return !left.Equals(right);
        }
        
        public override string ToString()
        {
            return $"GridNode({x}, {y})";
        }
    }
}

