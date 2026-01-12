using Frame.FixMath;
using Frame.Physics2D;

namespace Frame.ECS
{
    /// <summary>
    /// ECS版本的碰撞检测器（简化版：只支持不旋转的矩形和圆形）
    /// </summary>
    public static class CollisionDetectorECS
    {
        /// <summary>
        /// 检测两个形状是否碰撞
        /// </summary>
        public static bool CheckCollision(
            CollisionShapeComponent shapeA, FixVector2 posA,
            CollisionShapeComponent shapeB, FixVector2 posB,
            out Contact2D contact)
        {
            contact = default;

            // 根据形状类型进行碰撞检测
            if (shapeA.shapeType == ShapeType.Circle && shapeB.shapeType == ShapeType.Circle)
            {
                return CircleVsCircle(shapeA, posA, shapeB, posB, out contact);
            }
            else if (shapeA.shapeType == ShapeType.Circle && shapeB.shapeType == ShapeType.Box)
            {
                return CircleVsBox(shapeA, posA, shapeB, posB, out contact);
            }
            else if (shapeA.shapeType == ShapeType.Box && shapeB.shapeType == ShapeType.Circle)
            {
                // 交换顺序，使用CircleVsBox
                bool result = CircleVsBox(shapeB, posB, shapeA, posA, out contact);
                // 反转法向量
                if (result)
                {
                    contact.Normal = -contact.Normal;
                }
                return result;
            }
            else if (shapeA.shapeType == ShapeType.Box && shapeB.shapeType == ShapeType.Box)
            {
                return BoxVsBox(shapeA, posA, shapeB, posB, out contact);
            }

            return false;
        }

        /// <summary>
        /// 圆形与圆形碰撞检测
        /// </summary>
        private static bool CircleVsCircle(
            CollisionShapeComponent circleA, FixVector2 posA,
            CollisionShapeComponent circleB, FixVector2 posB,
            out Contact2D contact)
        {
            contact = default;

            // 计算圆心距离
            FixVector2 delta = posB - posA;
            Fix64 distanceSquared = delta.SqrMagnitude();
            Fix64 radiusSum = circleA.radius + circleB.radius;
            Fix64 radiusSumSquared = radiusSum * radiusSum;

            // 如果距离平方大于半径和平方，则不碰撞
            if (distanceSquared > radiusSumSquared)
                return false;

            // 计算实际距离
            Fix64 distance = Fix64.Sqrt(distanceSquared);

            // 计算法向量（从A指向B）
            FixVector2 normal;
            if (distance > Fix64.Zero)
            {
                normal = delta.Normalized();
            }
            else
            {
                // 如果两个圆完全重叠，使用默认方向
                normal = FixVector2.Right;
            }

            // 计算穿透深度
            Fix64 penetration = radiusSum - distance;

            // 计算接触点（在A的边界上）
            FixVector2 contactPoint = posA + normal * circleA.radius;

            contact = new Contact2D
            {
                Point = contactPoint,
                Normal = normal,
                Penetration = penetration
            };

            return true;
        }

        /// <summary>
        /// 圆形与矩形碰撞检测（不旋转，使用AABB）
        /// </summary>
        private static bool CircleVsBox(
            CollisionShapeComponent circle, FixVector2 circlePos,
            CollisionShapeComponent box, FixVector2 boxPos,
            out Contact2D contact)
        {
            contact = default;

            Fix64 halfWidth = box.size.x / Fix64.Two;
            Fix64 halfHeight = box.size.y / Fix64.Two;
            Fix64 boxLeft = boxPos.x - halfWidth;
            Fix64 boxRight = boxPos.x + halfWidth;
            Fix64 boxBottom = boxPos.y - halfHeight;
            Fix64 boxTop = boxPos.y + halfHeight;

            // 找到矩形上距离圆心最近的点
            Fix64 closestX = Fix64.Max(boxLeft, Fix64.Min(circlePos.x, boxRight));
            Fix64 closestY = Fix64.Max(boxBottom, Fix64.Min(circlePos.y, boxTop));

            FixVector2 closestPoint = new FixVector2(closestX, closestY);
            FixVector2 delta = closestPoint - circlePos;
            Fix64 distanceSquared = delta.SqrMagnitude();
            Fix64 radiusSquared = circle.radius * circle.radius;

            if (distanceSquared > radiusSquared)
                return false;

            Fix64 distance = Fix64.Sqrt(distanceSquared);
            FixVector2 normal;
            
            if (distance > Fix64.Zero)
            {
                normal = delta / distance;
            }
            else
            {
                // 圆心在矩形内部，找到最短退出方向
                Fix64 distToLeft = circlePos.x - boxLeft;
                Fix64 distToRight = boxRight - circlePos.x;
                Fix64 distToBottom = circlePos.y - boxBottom;
                Fix64 distToTop = boxTop - circlePos.y;

                Fix64 minDist = Fix64.Min(Fix64.Min(distToLeft, distToRight),
                    Fix64.Min(distToBottom, distToTop));

                if (minDist == distToLeft)
                    normal = FixVector2.Right;
                else if (minDist == distToRight)
                    normal = FixVector2.Left;
                else if (minDist == distToBottom)
                    normal = FixVector2.Up;
                else
                    normal = FixVector2.Down;
            }

            Fix64 penetration = circle.radius - distance;
            FixVector2 contactPoint = circlePos + normal * circle.radius;

            contact = new Contact2D
            {
                Point = contactPoint,
                Normal = normal,
                Penetration = penetration
            };

            return true;
        }

        /// <summary>
        /// 矩形与矩形碰撞检测（不旋转，使用AABB）
        /// </summary>
        private static bool BoxVsBox(
            CollisionShapeComponent boxA, FixVector2 posA,
            CollisionShapeComponent boxB, FixVector2 posB,
            out Contact2D contact)
        {
            contact = default;

            Fix64 halfWidthA = boxA.size.x / Fix64.Two;
            Fix64 halfHeightA = boxA.size.y / Fix64.Two;
            Fix64 leftA = posA.x - halfWidthA;
            Fix64 rightA = posA.x + halfWidthA;
            Fix64 bottomA = posA.y - halfHeightA;
            Fix64 topA = posA.y + halfHeightA;

            Fix64 halfWidthB = boxB.size.x / Fix64.Two;
            Fix64 halfHeightB = boxB.size.y / Fix64.Two;
            Fix64 leftB = posB.x - halfWidthB;
            Fix64 rightB = posB.x + halfWidthB;
            Fix64 bottomB = posB.y - halfHeightB;
            Fix64 topB = posB.y + halfHeightB;

            // AABB碰撞检测
            if (rightA < leftB || leftA > rightB || topA < bottomB || bottomA > topB)
                return false;

            // 计算重叠量
            Fix64 overlapX = Fix64.Min(rightA, rightB) - Fix64.Max(leftA, leftB);
            Fix64 overlapY = Fix64.Min(topA, topB) - Fix64.Max(bottomA, bottomB);

            FixVector2 normal;
            Fix64 penetration;
            FixVector2 contactPoint;

            // 选择最小重叠轴作为分离方向
            if (overlapX < overlapY)
            {
                penetration = overlapX;
                if (posA.x < posB.x)
                    normal = FixVector2.Right;
                else
                    normal = FixVector2.Left;

                Fix64 contactX = (Fix64.Max(leftA, leftB) + Fix64.Min(rightA, rightB)) / Fix64.Two;
                Fix64 contactY = (Fix64.Max(bottomA, bottomB) + Fix64.Min(topA, topB)) / Fix64.Two;
                contactPoint = new FixVector2(contactX, contactY);
            }
            else
            {
                penetration = overlapY;
                if (posA.y < posB.y)
                    normal = FixVector2.Up;
                else
                    normal = FixVector2.Down;

                Fix64 contactX = (Fix64.Max(leftA, leftB) + Fix64.Min(rightA, rightB)) / Fix64.Two;
                Fix64 contactY = (Fix64.Max(bottomA, bottomB) + Fix64.Min(topA, topB)) / Fix64.Two;
                contactPoint = new FixVector2(contactX, contactY);
            }

            contact = new Contact2D
            {
                Point = contactPoint,
                Normal = normal,
                Penetration = penetration
            };

            return true;
        }
    }
}


