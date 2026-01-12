using System;
using Frame.FixMath;

namespace Frame.Physics2D
{
    /// <summary>
    /// 无旋转矩形，用来宽相位 四叉树
    /// </summary>
    public struct FixRect : IEquatable<FixRect>, IComparable<FixRect>
    {
        // 核心字段：全部为int，无float
        public Fix64 X; // 左边界
        public Fix64 Y; // 下边界（和Unity Rect坐标系一致）
        public Fix64 Width; // 宽度（正数）
        public Fix64 Height; // 高度（正数）
        // 派生属性（只读，纯整数计算）
        public Fix64 Right => X + Width; // 右边界
        public Fix64 Top => Y + Height; // 上边界
        public Fix64 CenterX => X + Width / new Fix64(2); // 中心X
        public Fix64 CenterY => Y + Height / new Fix64(2); // 中心Y
        
        public FixVector2 Center => new FixVector2(CenterX, CenterY);

        // 构造函数（强制校验宽度/高度为正，避免无效数据）
        public FixRect(Fix64 x, Fix64 y, Fix64 width, Fix64 height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
        
        /// <summary>
        /// AABB 重叠检测（轴对齐包围盒，不考虑旋转）
        /// 用于宽相位快速筛选
        /// </summary>
        public bool Overlaps(FixRect other)
        {
            // 核心逻辑：不重叠的4种情况取反（无浮点、无舍入）
            return !(Right < other.X // 当前矩形在对方左侧
                     || X > other.Right // 当前矩形在对方右侧
                     || Top < other.Y // 当前矩形在对方下侧
                     || Y > other.Top); // 当前矩形在对方上侧
        }
        

        // 帧同步必备：重写Equals，避免引用比较
        public bool Equals(FixRect other)
        {
            return X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

        }

        public override bool Equals(object obj)
        {
            return obj is FixRect other && Equals(other);
        }

        // 帧同步必备：确定性哈希（基于整数字段）
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Width, Height);
        }

        // 方便调试：输出整数格式
        public override string ToString()
        {
            return $"X:{X}, Y:{Y}, W:{Width}, H:{Height}";
        }

        public int CompareTo(FixRect other)
        {
            if (this.Equals(other))
                return 0;
            return X.CompareTo(other.X);
        }

        /// <summary>
        /// 检查当前矩形是否完全包含另一个矩形
        /// </summary>
        public bool Contains(FixRect other)
        {
            return X <= other.X && Y <= other.Y &&
                   Right >= other.Right && Top >= other.Top;
        }

        /// <summary>
        /// 计算两个矩形的并集（包围盒）
        /// </summary>
        public static FixRect Union(FixRect other, FixRect b)
        {
            Fix64 minX = Fix64.Min(other.X, b.X);
            Fix64 minY = Fix64.Min(other.Y, b.Y);
            Fix64 maxRight = Fix64.Max(other.Right, b.Right);
            Fix64 maxTop = Fix64.Max(other.Top, b.Top);
            
            return new FixRect(minX, minY, maxRight - minX, maxTop - minY);
        }
        
        public void Union(FixRect other)
        {
            this.X = Fix64.Min(other.X, this.X);
            this.Y = Fix64.Min(other.Y, this.Y);
            this.Width = Fix64.Max(other.Right, this.Right) - X;
            this.Height = Fix64.Max(other.Top, this.Top) - Y;

        }
    }
}