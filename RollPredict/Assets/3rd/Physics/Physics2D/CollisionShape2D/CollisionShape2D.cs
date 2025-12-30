using Frame.FixMath;

namespace Frame.Physics2D
{
       /// <summary>
    /// 碰撞形状基类
    /// </summary>
    public abstract class CollisionShape2D
    {
        /// <summary>
        /// 获取形状在世界坐标下的边界矩形（用于宽相位检测）
        /// </summary>
        /// <param name="position">物体位置</param>
        /// <returns>边界矩形</returns>
        public abstract FixRect GetBounds(FixVector2 position);

        /// <summary>
        /// 检测两个形状是否碰撞
        /// </summary>
        /// <param name="shapeA">形状A</param>
        /// <param name="posA">形状A的位置</param>
        /// <param name="rotationA">形状A的旋转角度（弧度）</param>
        /// <param name="shapeB">形状B</param>
        /// <param name="posB">形状B的位置</param>
        /// <param name="rotationB">形状B的旋转角度（弧度）</param>
        /// <param name="contact">碰撞信息（如果碰撞）</param>
        /// <returns>是否碰撞</returns>
        public static bool CheckCollision(
            CollisionShape2D shapeA, FixVector2 posA,
            CollisionShape2D shapeB, FixVector2 posB,
            out Contact2D contact)
        {
            contact = default;

            // 根据形状类型进行碰撞检测
            if (shapeA is CircleShape2D circleA && shapeB is CircleShape2D circleB)
            {
                return CollisionDetector.CircleVsCircle(
                    circleA, posA,
                    circleB, posB,
                    out contact);
            }
            else if (shapeA is CircleShape2D circle && shapeB is BoxShape2D box)
            {
                return CollisionDetector.CircleVsBox(
                    circle, posA,
                    box, posB, 
                    out contact);
            }
            else if (shapeA is BoxShape2D boxA && shapeB is CircleShape2D circleShape)
            {
                // 交换顺序，使用CircleVsBox
                bool result = CollisionDetector.CircleVsBox(
                    circleShape, posB,
                    boxA, posA, 
                    out contact);
                // 反转法向量
                if (result)
                {
                    contact.Normal = -contact.Normal;
                }
                return result;
            }
            else if (shapeA is BoxShape2D boxShapeA && shapeB is BoxShape2D boxShapeB)
            {
                return CollisionDetector.BoxVsBox(
                    boxShapeA, posA, 
                    boxShapeB, posB,
                    out contact);
            }

            return false;
        }
    }
}