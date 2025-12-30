using System;
using Frame.FixMath;

namespace Frame.Physics2D
{
    /// <summary>
    /// 圆形碰撞形状
    /// </summary>
    public class CircleShape2D : CollisionShape2D
    {
        /// <summary>
        /// 半径
        /// </summary>
        public Fix64 Radius { get; set; }

        public CircleShape2D(Fix64 radius)
        {
            if (radius <= Fix64.Zero)
                throw new ArgumentException("半径必须大于0", nameof(radius));
            Radius = radius;
        }

        public override FixRect GetBounds(FixVector2 position)
        {
            return new FixRect(
                position.x - Radius,
                position.y - Radius,
                Radius * Fix64.Two,
                Radius * Fix64.Two
            );
        }
    }
}