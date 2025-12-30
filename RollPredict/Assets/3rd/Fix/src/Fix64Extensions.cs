using System;
using UnityEngine;

namespace Frame.FixMath
{
    /// <summary>
    /// Fix64 自定义扩展
    /// 包含用户自定义的方法和功能扩展
    /// 使用 partial struct 来扩展 Fix64，保持原始库文件不变
    /// </summary>
    public partial struct Fix64
    {
        public static readonly Fix64 Rad2Deg = (Fix64)57.295779513082320876798154814105M;
        public static readonly Fix64 Deg2Rad = (Fix64)0.017453292519943295769236907684886M;
        public static readonly Fix64 Two = (Fix64)2;

        /// <summary>
        /// Returns the smaller of two Fix64 values.
        /// </summary>
        public static Fix64 Min(Fix64 x, Fix64 y)
        {
            return x.m_rawValue < y.m_rawValue ? x : y;
        }

        /// <summary>
        /// Returns the larger of two Fix64 values.
        /// </summary>
        public static Fix64 Max(Fix64 x, Fix64 y)
        {
            return x.m_rawValue > y.m_rawValue ? x : y;
        }

        public static Fix64 Clamp(Fix64 value, Fix64 min, Fix64 max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }


    /// <summary>
    /// 定点数二维向量（用于帧同步）
    /// </summary>
    public partial struct FixVector2 : IEquatable<FixVector2>
    {
        public Fix64 x;
        public Fix64 y;

        public static readonly FixVector2 Zero = new FixVector2(Fix64.Zero, Fix64.Zero);
        public static readonly FixVector2 One = new FixVector2(Fix64.One, Fix64.One);
        public static readonly FixVector2 Up = new FixVector2(Fix64.Zero, Fix64.One);
        public static readonly FixVector2 Down = new FixVector2(Fix64.Zero, -Fix64.One);
        public static readonly FixVector2 Left = new FixVector2(-Fix64.One, Fix64.Zero);
        public static readonly FixVector2 Right = new FixVector2(Fix64.One, Fix64.Zero);

        public FixVector2(Fix64 x, Fix64 y)
        {
            this.x = x;
            this.y = y;
        }

        public FixVector2(Vector2 vec)
        {
            this.x = (Fix64)vec.x;
            this.y = (Fix64)vec.y;
        }

        // 运算符重载
        public static FixVector2 operator +(FixVector2 a, FixVector2 b)
        {
            return new FixVector2(a.x + b.x, a.y + b.y);
        }

        public static FixVector2 operator -(FixVector2 a, FixVector2 b)
        {
            return new FixVector2(a.x - b.x, a.y - b.y);
        }

        public static FixVector2 operator -(FixVector2 a)
        {
            return new FixVector2(-a.x, -a.y);
        }

        public static FixVector2 operator *(FixVector2 a, Fix64 scalar)
        {
            return new FixVector2(a.x * scalar, a.y * scalar);
        }

        public static FixVector2 operator *(Fix64 scalar, FixVector2 a)
        {
            return new FixVector2(a.x * scalar, a.y * scalar);
        }

        public static FixVector2 operator /(FixVector2 a, Fix64 scalar)
        {
            return new FixVector2(a.x / scalar, a.y / scalar);
        }

        // 类型转换
        public static explicit operator Vector2(FixVector2 value)
        {
            return new Vector2((float)value.x, (float)value.y);
        }

        public static explicit operator FixVector2(Vector2 value)
        {
            return new FixVector2(value);
        }

        // 向量运算
        /// <summary>
        /// 计算两个向量之间的距离的平方（避免开方，性能更好）
        /// </summary>
        public static Fix64 SqrDistance(FixVector2 a, FixVector2 b)
        {
            Fix64 dx = a.x - b.x;
            Fix64 dy = a.y - b.y;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// 计算两个向量之间的距离
        /// </summary>
        public static Fix64 Distance(FixVector2 a, FixVector2 b)
        {
            return Fix64.Sqrt(SqrDistance(a, b));
        }

        /// <summary>
        /// 计算向量的平方长度（避免开方，性能更好）
        /// </summary>
        public Fix64 SqrMagnitude()
        {
            return x * x + y * y;
        }

        /// <summary>
        /// 计算向量的长度
        /// </summary>
        public Fix64 Magnitude()
        {
            return Fix64.Sqrt(SqrMagnitude());
        }


        /// <summary>
        /// 返回归一化后的向量（单位向量）
        /// 如果向量为零向量，返回零向量
        /// </summary>
        public FixVector2 Normalized()
        {
            Fix64 magnitude = Magnitude();
            if (magnitude == Fix64.Zero)
                return Zero;
            return new FixVector2(x / magnitude, y / magnitude);
        }


        /// <summary>
        /// 归一化向量（修改自身）
        /// 如果向量为零向量，不做任何操作
        /// </summary>
        public void Normalize()
        {
            Fix64 magnitude = Magnitude();
            if (magnitude != Fix64.Zero)
            {
                x = x / magnitude;
                y = y / magnitude;
            }
        }

        /// <summary>
        /// 计算两个向量的点积
        /// </summary>
        public static Fix64 Dot(FixVector2 a, FixVector2 b)
        {
            return a.x * b.x + a.y * b.y;
        }

        /// <summary>
        /// 计算两个向量的叉积（返回标量，表示 z 分量）
        /// </summary>
        public static Fix64 Cross(FixVector2 a, FixVector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        /// <summary>
        /// 线性插值：Lerp(a, b, t) = a + (b - a) * t
        /// </summary>
        public static FixVector2 Lerp(FixVector2 a, FixVector2 b, Fix64 t)
        {
            return a + (b - a) * t;
        }

        /// <summary>
        /// 限制向量的长度不超过指定值
        /// </summary>
        public static FixVector2 ClampMagnitude(FixVector2 vector, Fix64 maxLength)
        {
            Fix64 sqrMagnitude = vector.SqrMagnitude();
            if (sqrMagnitude > maxLength * maxLength)
            {
                return vector.Normalized() * maxLength;
            }

            return vector;
        }

        // IEquatable 实现
        public override bool Equals(object obj)
        {
            return obj is FixVector2 other && Equals(other);
        }

        public bool Equals(FixVector2 other)
        {
            return x == other.x && y == other.y;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y);
        }

        public override string ToString()
        {
            return $"({x}, {y})";
        }

        public static bool operator ==(FixVector2 a, FixVector2 b)
        {
            return a.x == b.x && a.y == b.y;
        }

        public static bool operator !=(FixVector2 a, FixVector2 b)
        {
            return !(a == b);
        }


        public Fix64 ToRotation()
        {
            // 1. 调用Atan2计算弧度（注意参数顺序：Atan2(y, x)）
            Fix64 rad = Fix64.Atan2(y, x);
            // 2. 转换为角度（弧度 × (180/π)）
            Fix64 deg = rad * Fix64.Rad2Deg;
            // 3. 将负角度转换为正角度（比如-90度 → 270度，范围统一为0~360度）

            deg += new Fix64(270);

            return deg;
        }
    }


    /// <summary>
    /// 定点数三维向量（用于帧同步）
    /// </summary>
    public struct FixVector3 : IEquatable<FixVector3>
    {
        public Fix64 x;
        public Fix64 y;
        public Fix64 z;

        public static readonly FixVector3 Zero = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.Zero);
        public static readonly FixVector3 One = new FixVector3(Fix64.One, Fix64.One, Fix64.One);
        public static readonly FixVector3 Up = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
        public static readonly FixVector3 Down = new FixVector3(Fix64.Zero, -Fix64.One, Fix64.Zero);
        public static readonly FixVector3 Left = new FixVector3(-Fix64.One, Fix64.Zero, Fix64.Zero);
        public static readonly FixVector3 Right = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
        public static readonly FixVector3 Forward = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
        public static readonly FixVector3 Back = new FixVector3(Fix64.Zero, Fix64.Zero, -Fix64.One);

        public FixVector3(Fix64 x, Fix64 y, Fix64 z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public FixVector3(Vector3 vec)
        {
            this.x = (Fix64)vec.x;
            this.y = (Fix64)vec.y;
            this.z = (Fix64)vec.z;
        }

        // 运算符重载
        public static FixVector3 operator +(FixVector3 a, FixVector3 b)
        {
            return new FixVector3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static FixVector3 operator -(FixVector3 a, FixVector3 b)
        {
            return new FixVector3(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        public static FixVector3 operator -(FixVector3 a)
        {
            return new FixVector3(-a.x, -a.y, -a.z);
        }

        public static FixVector3 operator *(FixVector3 a, Fix64 scalar)
        {
            return new FixVector3(a.x * scalar, a.y * scalar, a.z * scalar);
        }

        public static FixVector3 operator *(Fix64 scalar, FixVector3 a)
        {
            return new FixVector3(a.x * scalar, a.y * scalar, a.z * scalar);
        }

        public static FixVector3 operator /(FixVector3 a, Fix64 scalar)
        {
            return new FixVector3(a.x / scalar, a.y / scalar, a.z / scalar);
        }

        // 类型转换
        public static explicit operator Vector3(FixVector3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        public static explicit operator FixVector3(Vector3 value)
        {
            return new FixVector3(value);
        }

        // 向量运算
        /// <summary>
        /// 计算两个向量之间的距离的平方（避免开方，性能更好）
        /// </summary>
        public static Fix64 SqrDistance(FixVector3 a, FixVector3 b)
        {
            Fix64 dx = a.x - b.x;
            Fix64 dy = a.y - b.y;
            Fix64 dz = a.z - b.z;
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>
        /// 计算两个向量之间的距离
        /// </summary>
        public static Fix64 Distance(FixVector3 a, FixVector3 b)
        {
            return Fix64.Sqrt(SqrDistance(a, b));
        }

        /// <summary>
        /// 计算向量的平方长度（避免开方，性能更好）
        /// </summary>
        public Fix64 SqrMagnitude()
        {
            return x * x + y * y + z * z;
        }

        /// <summary>
        /// 计算向量的长度
        /// </summary>
        public Fix64 Magnitude()
        {
            return Fix64.Sqrt(SqrMagnitude());
        }

        /// <summary>
        /// 返回归一化后的向量（单位向量）
        /// 如果向量为零向量，返回零向量
        /// </summary>
        public FixVector3 Normalized()
        {
            Fix64 magnitude = Magnitude();
            if (magnitude == Fix64.Zero)
                return Zero;
            return new FixVector3(x / magnitude, y / magnitude, z / magnitude);
        }

        /// <summary>
        /// 归一化向量（修改自身）
        /// 如果向量为零向量，不做任何操作
        /// </summary>
        public void Normalize()
        {
            Fix64 magnitude = Magnitude();
            if (magnitude != Fix64.Zero)
            {
                x = x / magnitude;
                y = y / magnitude;
                z = z / magnitude;
            }
        }

        /// <summary>
        /// 计算两个向量的点积
        /// </summary>
        public static Fix64 Dot(FixVector3 a, FixVector3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        /// <summary>
        /// 计算两个向量的叉积
        /// </summary>
        public static FixVector3 Cross(FixVector3 a, FixVector3 b)
        {
            return new FixVector3(
                a.y * b.z - a.z * b.y,
                a.z * b.x - a.x * b.z,
                a.x * b.y - a.y * b.x
            );
        }

        /// <summary>
        /// 线性插值：Lerp(a, b, t) = a + (b - a) * t
        /// </summary>
        public static FixVector3 Lerp(FixVector3 a, FixVector3 b, Fix64 t)
        {
            return a + (b - a) * t;
        }

        /// <summary>
        /// 限制向量的长度不超过指定值
        /// </summary>
        public static FixVector3 ClampMagnitude(FixVector3 vector, Fix64 maxLength)
        {
            Fix64 sqrMagnitude = vector.SqrMagnitude();
            if (sqrMagnitude > maxLength * maxLength)
            {
                return vector.Normalized() * maxLength;
            }

            return vector;
        }

        // IEquatable 实现
        public override bool Equals(object obj)
        {
            return obj is FixVector3 other && Equals(other);
        }

        public bool Equals(FixVector3 other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y, z);
        }

        public override string ToString()
        {
            return $"({x}, {y}, {z})";
        }

        public static bool operator ==(FixVector3 a, FixVector3 b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }

        public static bool operator !=(FixVector3 a, FixVector3 b)
        {
            return !(a == b);
        }
    }
}