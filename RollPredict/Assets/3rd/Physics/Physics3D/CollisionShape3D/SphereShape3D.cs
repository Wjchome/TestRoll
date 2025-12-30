using System;
using Frame.FixMath;

namespace Frame.Physics3D
{
    /// <summary>
    /// 球体碰撞形状
    /// </summary>
    public class SphereShape3D : CollisionShape3D
    {
        /// <summary>
        /// 半径
        /// </summary>
        public Fix64 Radius { get; set; }

        public SphereShape3D(Fix64 radius)
        {
            if (radius <= Fix64.Zero)
                throw new ArgumentException("半径必须大于0", nameof(radius));
            Radius = radius;
        }

        public override FixBounds GetBounds(FixVector3 position)
        {
            Fix64 diameter = Radius * Fix64.Two;
            return new FixBounds(
                new FixVector3(
                    position.x - Radius,
                    position.y - Radius,
                    position.z - Radius
                ),
                new FixVector3(
                    position.x + Radius,
                    position.y + Radius,
                    position.z + Radius
                )
            );
        }
    }
}

