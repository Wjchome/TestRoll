using System;
using Frame.FixMath;

namespace Frame.Physics3D
{
    /// <summary>
    /// 长方体碰撞形状（支持旋转，OBB - Oriented Bounding Box）
    /// </summary>
    public class BoxShape3D : CollisionShape3D
    {
        /// <summary>
        /// 宽度（X轴方向）
        /// </summary>
        public Fix64 Width { get; set; }

        /// <summary>
        /// 高度（Y轴方向）
        /// </summary>
        public Fix64 Height { get; set; }

        /// <summary>
        /// 长度（Z轴方向）
        /// </summary>
        public Fix64 Length { get; set; }

        /// <summary>
        /// 旋转（欧拉角，弧度）
        /// </summary>
        public FixVector3 Rotation { get; set; } = FixVector3.Zero;

        public BoxShape3D(Fix64 width, Fix64 height, Fix64 length)
        {
            if (width <= Fix64.Zero || height <= Fix64.Zero || length <= Fix64.Zero)
                throw new ArgumentException("宽度、高度和长度必须大于0");
            Width = width;
            Height = height;
            Length = length;
        }

        public BoxShape3D(Fix64 width, Fix64 height, Fix64 length, FixVector3 rotation)
            : this(width, height, length)
        {
            Rotation = rotation;
        }

        /// <summary>
        /// 获取旋转后的AABB（用于宽相位检测）
        /// 计算旋转后的8个顶点，然后找到最小/最大点
        /// </summary>
        public override FixBounds GetBounds(FixVector3 position)
        {
            // 有旋转：计算旋转后的8个顶点
            Fix64 halfWidth = Width / Fix64.Two;
            Fix64 halfHeight = Height / Fix64.Two;
            Fix64 halfLength = Length / Fix64.Two;
            // 如果无旋转，使用快速AABB
            if (Rotation.x == Fix64.Zero && Rotation.y == Fix64.Zero && Rotation.z == Fix64.Zero)
            {

                return new FixBounds(
                    new FixVector3(
                        position.x - halfWidth,
                        position.y - halfHeight,
                        position.z - halfLength
                    ),
                    new FixVector3(
                        position.x + halfWidth,
                        position.y + halfHeight,
                        position.z + halfLength
                    ),
                    FixBoundsInitType.MinMax
                );
            }

        

            // 本地空间的8个顶点
            FixVector3[] localVertices = new FixVector3[]
            {
                new FixVector3(-halfWidth, -halfHeight, -halfLength), // 左下后
                new FixVector3(halfWidth, -halfHeight, -halfLength),  // 右下后
                new FixVector3(halfWidth, halfHeight, -halfLength),   // 右上后
                new FixVector3(-halfWidth, halfHeight, -halfLength),  // 左上后
                new FixVector3(-halfWidth, -halfHeight, halfLength),   // 左下前
                new FixVector3(halfWidth, -halfHeight, halfLength),    // 右下前
                new FixVector3(halfWidth, halfHeight, halfLength),      // 右上前
                new FixVector3(-halfWidth, halfHeight, halfLength)     // 左上前
            };

            // 旋转矩阵（欧拉角：ZYX顺序）
            Fix64 cx = Fix64.Cos(Rotation.x);
            Fix64 sx = Fix64.Sin(Rotation.x);
            Fix64 cy = Fix64.Cos(Rotation.y);
            Fix64 sy = Fix64.Sin(Rotation.y);
            Fix64 cz = Fix64.Cos(Rotation.z);
            Fix64 sz = Fix64.Sin(Rotation.z);

            // 旋转矩阵（ZYX顺序）
            // R = Rz * Ry * Rx
            Fix64 m00 = cz * cy;
            Fix64 m01 = cz * sy * sx - sz * cx;
            Fix64 m02 = cz * sy * cx + sz * sx;
            Fix64 m10 = sz * cy;
            Fix64 m11 = sz * sy * sx + cz * cx;
            Fix64 m12 = sz * sy * cx - cz * sx;
            Fix64 m20 = -sy;
            Fix64 m21 = cy * sx;
            Fix64 m22 = cy * cx;

            // 旋转并平移到世界空间
            Fix64 minX = Fix64.MaxValue, minY = Fix64.MaxValue, minZ = Fix64.MaxValue;
            Fix64 maxX = Fix64.MinValue, maxY = Fix64.MinValue, maxZ = Fix64.MinValue;

            for (int i = 0; i < localVertices.Length; i++)
            {
                FixVector3 v = localVertices[i];
                // 应用旋转矩阵
                FixVector3 rotated = new FixVector3(
                    m00 * v.x + m01 * v.y + m02 * v.z,
                    m10 * v.x + m11 * v.y + m12 * v.z,
                    m20 * v.x + m21 * v.y + m22 * v.z
                );
                // 平移到世界空间
                FixVector3 world = position + rotated;

                minX = Fix64.Min(minX, world.x);
                minY = Fix64.Min(minY, world.y);
                minZ = Fix64.Min(minZ, world.z);
                maxX = Fix64.Max(maxX, world.x);
                maxY = Fix64.Max(maxY, world.y);
                maxZ = Fix64.Max(maxZ, world.z);
            }

            return new FixBounds(
                new FixVector3(minX, minY, minZ),
                new FixVector3(maxX, maxY, maxZ),
                FixBoundsInitType.MinMax
            );
        }

        /// <summary>
        /// 获取旋转后的局部轴（用于SAT碰撞检测）
        /// </summary>
        internal void GetAxes(out FixVector3 axisX, out FixVector3 axisY, out FixVector3 axisZ)
        {
            // 旋转矩阵
            Fix64 cx = Fix64.Cos(Rotation.x);
            Fix64 sx = Fix64.Sin(Rotation.x);
            Fix64 cy = Fix64.Cos(Rotation.y);
            Fix64 sy = Fix64.Sin(Rotation.y);
            Fix64 cz = Fix64.Cos(Rotation.z);
            Fix64 sz = Fix64.Sin(Rotation.z);

            // 旋转矩阵（ZYX顺序）
            Fix64 m00 = cz * cy;
            Fix64 m01 = cz * sy * sx - sz * cx;
            Fix64 m02 = cz * sy * cx + sz * sx;
            Fix64 m10 = sz * cy;
            Fix64 m11 = sz * sy * sx + cz * cx;
            Fix64 m12 = sz * sy * cx - cz * sx;
            Fix64 m20 = -sy;
            Fix64 m21 = cy * sx;
            Fix64 m22 = cy * cx;

            // 局部轴（单位向量）
            axisX = new FixVector3(m00, m10, m20).Normalized();
            axisY = new FixVector3(m01, m11, m21).Normalized();
            axisZ = new FixVector3(m02, m12, m22).Normalized();
        }
    }
}

