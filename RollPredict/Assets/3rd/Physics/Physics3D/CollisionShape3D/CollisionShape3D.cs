using Frame.FixMath;

namespace Frame.Physics3D
{
    /// <summary>
    /// 碰撞形状基类（3D）
    /// </summary>
    public abstract class CollisionShape3D
    {
        /// <summary>
        /// 获取形状在世界坐标下的边界框（用于宽相位检测）
        /// </summary>
        /// <param name="position">物体位置</param>
        /// <returns>边界框</returns>
        public abstract FixBounds GetBounds(FixVector3 position);

        /// <summary>
        /// 检测两个形状是否碰撞
        /// </summary>
        /// <param name="shapeA">形状A</param>
        /// <param name="posA">形状A的位置</param>
        /// <param name="shapeB">形状B</param>
        /// <param name="posB">形状B的位置</param>
        /// <param name="contact">碰撞信息（如果碰撞）</param>
        /// <returns>是否碰撞</returns>
        public static bool CheckCollision(
            CollisionShape3D shapeA, FixVector3 posA,
            CollisionShape3D shapeB, FixVector3 posB,
            out Contact3D contact)
        {
            contact = default;

            // 根据形状类型进行碰撞检测
            if (shapeA is SphereShape3D sphereA && shapeB is SphereShape3D sphereB)
            {
                return CollisionDetector3D.SphereVsSphere(
                    sphereA, posA,
                    sphereB, posB,
                    out contact);
            }
            else if (shapeA is SphereShape3D sphere && shapeB is BoxShape3D box)
            {
                return CollisionDetector3D.SphereVsBox(
                    sphere, posA,
                    box, posB,
                    out contact);
            }
            else if (shapeA is BoxShape3D boxA && shapeB is SphereShape3D sphereShape)
            {
                // 交换顺序，使用SphereVsBox
                bool result = CollisionDetector3D.SphereVsBox(
                    sphereShape, posB,
                    boxA, posA,
                    out contact);
                // 反转法向量
                if (result)
                {
                    contact.Normal = -contact.Normal;
                }
                return result;
            }
            else if (shapeA is BoxShape3D boxShapeA && shapeB is BoxShape3D boxShapeB)
            {
                return CollisionDetector3D.BoxVsBox(
                    boxShapeA, posA,
                    boxShapeB, posB,
                    out contact);
            }

            return false;
        }
    }
}

