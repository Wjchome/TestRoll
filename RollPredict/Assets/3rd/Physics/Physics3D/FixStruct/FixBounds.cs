using Frame.FixMath;

namespace Frame.Physics3D
{
    public enum FixBoundsInitType
    {
        MinMax,
        Center,
    }


    /// <summary>
    /// 3D轴对齐包围盒（AABB）
    /// </summary>
    public struct FixBounds
    {
        /// <summary>
        /// 最小点（左下前角）
        /// </summary>
        public FixVector3 Min;

        /// <summary>
        /// 最大点（右上后角）
        /// </summary>
        public FixVector3 Max;

        public FixBounds(FixVector3 value1, FixVector3 value2, FixBoundsInitType initType = FixBoundsInitType.MinMax)
        {
            this.Min = default;
            this.Max = default;
            if (initType == FixBoundsInitType.MinMax)
            {
                this.Min = value1;
                this.Max = value2;
            }
            else if (initType == FixBoundsInitType.Center)
            {
                FixVector3 halfSize = value2 / Fix64.Two;
                this.Min = value1 - halfSize;
                this.Max = value1 + halfSize;
            }
        }


        /// <summary>
        /// 中心点
        /// </summary>
        public FixVector3 Center => (Min + Max) / Fix64.Two;

        /// <summary>
        /// 尺寸
        /// </summary>
        public FixVector3 Size => Max - Min;

        /// <summary>
        /// 检查两个包围盒是否相交
        /// </summary>
        public bool Intersects(FixBounds other)
        {
            if (other.Min.x > Max.x || other.Min.y > Max.y || other.Min.z > Max.z)
                return false;
            if (Min.x > other.Max.x || Min.y > other.Max.y || Min.z > other.Max.z)
                return false;
            return true;
        }

        /// <summary>
        /// 检查点是否在包围盒内
        /// </summary>
        public bool Contains(FixVector3 point)
        {
            return point.x >= Min.x && point.x <= Max.x &&
                   point.y >= Min.y && point.y <= Max.y &&
                   point.z >= Min.z && point.z <= Max.z;
        }

        /// <summary>
        /// 扩展包围盒以包含指定点
        /// </summary>
        public void Encapsulate(FixVector3 point)
        {
            Min.x = Fix64.Min(Min.x, point.x);
            Min.y = Fix64.Min(Min.y, point.y);
            Min.z = Fix64.Min(Min.z, point.z);
            Max.x = Fix64.Max(Max.x, point.x);
            Max.y = Fix64.Max(Max.y, point.y);
            Max.z = Fix64.Max(Max.z, point.z);
        }

        /// <summary>
        /// 扩展包围盒以包含另一个包围盒
        /// </summary>
        public void Encapsulate(FixBounds bounds)
        {
            Min.x = Fix64.Min(Min.x, bounds.Min.x);
            Min.y = Fix64.Min(Min.y, bounds.Min.y);
            Min.z = Fix64.Min(Min.z, bounds.Min.z);
            Max.x = Fix64.Max(Max.x, bounds.Max.x);
            Max.y = Fix64.Max(Max.y, bounds.Max.y);
            Max.z = Fix64.Max(Max.z, bounds.Max.z);
        }

        public override bool Equals(object obj)
        {
            return obj is FixBounds other && Equals(other);
        }

        public bool Equals(FixBounds other)
        {
            return Min.Equals(other.Min) && Max.Equals(other.Max);
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(Min, Max);
        }

        public static bool operator ==(FixBounds a, FixBounds b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(FixBounds a, FixBounds b)
        {
            return !a.Equals(b);
        }

        public override string ToString()
        {
            return $"Bounds(Min: {Min}, Max: {Max})";
        }
    }
}