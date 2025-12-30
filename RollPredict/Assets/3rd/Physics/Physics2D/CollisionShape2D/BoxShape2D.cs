using System;
using Frame.FixMath;

namespace Frame.Physics2D
{
    /// <summary>
    /// 矩形碰撞形状（支持旋转）
    /// </summary>
    public class BoxShape2D : CollisionShape2D
    {
        /// <summary>
        /// 宽度
        /// </summary>
        public Fix64 Width { get; set; }

        /// <summary>
        /// 高度
        /// </summary>
        public Fix64 Height { get; set; }

        /// <summary>
        /// 旋转
        /// </summary>
        public Fix64 Rotation { get; set; }

        public BoxShape2D(Fix64 width, Fix64 height, Fix64 rotation)
        {
            if (width <= Fix64.Zero || height <= Fix64.Zero)
                throw new ArgumentException("宽度和高度必须大于0", nameof(width));
            Width = width;
            Height = height;
            Rotation = rotation;
        }

        public override FixRect GetBounds(FixVector2 position)
        {
            // 步骤1：计算矩形的四个本地顶点（中心在原点）
            Fix64 halfWidth = Width / Fix64.Two;
            Fix64 halfHeight = Height / Fix64.Two;
            // 四个本地顶点：右上、右下、左下、左上（顺序不影响，遍历即可）
            FixVector2[] localVertices = new FixVector2[]
            {
                new FixVector2(halfWidth, halfHeight),
                new FixVector2(halfWidth, -halfHeight),
                new FixVector2(-halfWidth, -halfHeight),
                new FixVector2(-halfWidth, halfHeight)
            };

            // 步骤2：计算旋转的正弦和余弦值（用于顶点旋转计算）
            Fix64 cos = Fix64.Cos(Rotation);
            Fix64 sin = Fix64.Sin(Rotation);

            Fix64 minX = Fix64.MaxValue, minY = Fix64.MaxValue;
            Fix64 maxX = Fix64.MinValue, maxY = Fix64.MinValue;
            for (int i = 0; i < localVertices.Length; i++)
            {
                FixVector2 worldVertex = RotateAndTranslate(localVertices[i], cos, sin, position);
                minX = Fix64.Min(minX, worldVertex.x);
                maxX = Fix64.Max(maxX, worldVertex.x);
                minY = Fix64.Min(minY, worldVertex.y);
                maxY = Fix64.Max(maxY, worldVertex.y);
            }
            
            return new FixRect(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// 辅助方法：将本地顶点旋转并平移到世界空间
        /// </summary>
        /// <param name="localVertex">本地顶点</param>
        /// <param name="cos">旋转角的余弦值</param>
        /// <param name="sin">旋转角的正弦值</param>
        /// <param name="position">世界位置（平移量）</param>
        /// <returns>世界空间顶点</returns>
        private FixVector2 RotateAndTranslate(FixVector2 localVertex, Fix64 cos, Fix64 sin, FixVector2 position)
        {
            // 旋转公式：x' = x*cos - y*sin; y' = x*sin + y*cos
            Fix64 rotatedX = localVertex.x * cos - localVertex.y * sin;
            Fix64 rotatedY = localVertex.x * sin + localVertex.y * cos; // 修复：应该是 x*sin，不是 y*sin
            // 平移：加上世界位置
            return new FixVector2(rotatedX + position.x, rotatedY + position.y);
        }
    }
}