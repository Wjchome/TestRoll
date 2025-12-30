using Frame.FixMath;

namespace Frame.Physics2D
{
    /// <summary>
    /// 碰撞检测器（实现各种形状之间的碰撞检测）
    /// </summary>
    public static class CollisionDetector
    {
        /// <summary>
        /// 圆形与圆形碰撞检测
        /// </summary>
        public static bool CircleVsCircle(
            CircleShape2D circleA, FixVector2 posA,
            CircleShape2D circleB, FixVector2 posB,
            out Contact2D contact)
        {
            contact = default;

            // 计算圆心距离
            FixVector2 delta = posB - posA;
            Fix64 distanceSquared = delta.SqrMagnitude();
            Fix64 radiusSum = circleA.Radius + circleB.Radius;
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
            FixVector2 contactPoint = posA + normal * circleA.Radius;

            contact = new Contact2D
            {
                Point = contactPoint,
                Normal = normal,
                Penetration = penetration
            };

            return true;
        }

        /// <summary>
        /// 圆形与矩形碰撞检测（支持旋转矩形）
        /// </summary>
        public static bool CircleVsBox(
            CircleShape2D circle, FixVector2 circlePos,
            BoxShape2D box, FixVector2 boxPos,
            out Contact2D contact)
        {
            contact = default;

            // 如果矩形没有旋转，使用快速AABB检测
            if (box.Rotation == Fix64.Zero)
            {
                return CircleVsBoxAABB(circle, circlePos, box, boxPos, out contact);
            }

            // 旋转矩形：将圆心转换到矩形的局部坐标系
            Fix64 centerX = boxPos.x;
            Fix64 centerY = boxPos.y;
            Fix64 halfWidth = box.Width / Fix64.Two;
            Fix64 halfHeight = box.Height / Fix64.Two;

            // 将圆心平移到以矩形中心为原点
            Fix64 localX = circlePos.x - centerX;
            Fix64 localY = circlePos.y - centerY;

            // 反向旋转（将点从世界坐标系转换到矩形局部坐标系）
            Fix64 cosToLocal = Fix64.Cos(-box.Rotation);
            Fix64 sinToLocal = Fix64.Sin(-box.Rotation);
            Fix64 rotatedX = localX * cosToLocal - localY * sinToLocal;
            Fix64 rotatedY = localX * sinToLocal + localY * cosToLocal;

            // 在局部坐标系中找到最近点（矩形在局部坐标系中是轴对齐的）
            Fix64 closestLocalX = Fix64.Max(-halfWidth, Fix64.Min(rotatedX, halfWidth));
            Fix64 closestLocalY = Fix64.Max(-halfHeight, Fix64.Min(rotatedY, halfHeight));

            // 计算局部坐标系中的距离（从圆心指向最近点，即从圆心指向矩形）
            Fix64 dx = closestLocalX - rotatedX;
            Fix64 dy = closestLocalY - rotatedY;
            Fix64 distanceSquared = dx * dx + dy * dy;
            Fix64 radiusSquared = circle.Radius * circle.Radius;

            if (distanceSquared > radiusSquared)
                return false;

            Fix64 distance = Fix64.Sqrt(distanceSquared);

            // 计算法向量（在局部坐标系中，从圆心指向矩形）
            FixVector2 localNormal;
            if (distance > Fix64.Zero)
            {
                localNormal = new FixVector2(dx / distance, dy / distance);
            }
            else
            {
                // 圆心在矩形内部，找到最短退出方向（从圆心指向矩形边界）
                Fix64 distToLeft = rotatedX + halfWidth;
                Fix64 distToRight = halfWidth - rotatedX;
                Fix64 distToBottom = rotatedY + halfHeight;
                Fix64 distToTop = halfHeight - rotatedY;

                Fix64 minDist = Fix64.Min(Fix64.Min(distToLeft, distToRight),
                    Fix64.Min(distToBottom, distToTop));

                if (minDist == distToLeft)
                    localNormal = FixVector2.Right;  // 从圆心指向右（矩形在右边）
                else if (minDist == distToRight)
                    localNormal = FixVector2.Left;   // 从圆心指向左（矩形在左边）
                else if (minDist == distToBottom)
                    localNormal = FixVector2.Up;     // 从圆心指向上（矩形在上边）
                else
                    localNormal = FixVector2.Down;   // 从圆心指向下（矩形在下边）
            }

            // 将法向量转换回世界坐标系
            // 注意：法向量需要用正向旋转（从局部到世界），而不是反向旋转
            // 因为法向量的转换需要用转置矩阵，而转置矩阵等于正向旋转矩阵
            Fix64 cosToWorld = Fix64.Cos(box.Rotation);
            Fix64 sinToWorld = Fix64.Sin(box.Rotation);
            Fix64 normalX = localNormal.x * cosToWorld - localNormal.y * sinToWorld;
            Fix64 normalY = localNormal.x * sinToWorld + localNormal.y * cosToWorld;
            FixVector2 normal = new FixVector2(normalX, normalY);

            // 计算穿透深度
            Fix64 penetration = circle.Radius - distance;

            // 接触点在圆上
            FixVector2 contactPoint = circlePos + normal * circle.Radius;

            contact = new Contact2D
            {
                Point = contactPoint,
                Normal = normal,
                Penetration = penetration
            };

            return true;
        }

        /// <summary>
        /// 圆形与轴对齐矩形碰撞检测（快速版本）
        /// </summary>
        private static bool CircleVsBoxAABB(
            CircleShape2D circle, FixVector2 circlePos,
            BoxShape2D box, FixVector2 boxPos,
            out Contact2D contact)
        {
            contact = default;

            Fix64 halfWidth = box.Width / Fix64.Two;
            Fix64 halfHeight = box.Height / Fix64.Two;
            Fix64 boxLeft = boxPos.x - halfWidth;
            Fix64 boxRight = boxPos.x + halfWidth;
            Fix64 boxBottom = boxPos.y - halfHeight;
            Fix64 boxTop = boxPos.y + halfHeight;

            Fix64 closestX = Fix64.Max(boxLeft, Fix64.Min(circlePos.x, boxRight));
            Fix64 closestY = Fix64.Max(boxBottom, Fix64.Min(circlePos.y, boxTop));

            FixVector2 closestPoint = new FixVector2(closestX, closestY);
            FixVector2 delta = closestPoint - circlePos;
            Fix64 distanceSquared = delta.SqrMagnitude();
            Fix64 radiusSquared = circle.Radius * circle.Radius;

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

            Fix64 penetration = circle.Radius - distance;
            FixVector2 contactPoint = circlePos + normal * circle.Radius;

            contact = new Contact2D
            {
                Point = contactPoint,
                Normal = normal,
                Penetration = penetration
            };

            return true;
        }

        /// <summary>
        /// 矩形与矩形碰撞检测（支持旋转矩形）
        /// 直接使用FixRect.Overlaps()，复用其SAT实现，避免重复计算
        /// </summary>
        public static bool BoxVsBox(
            BoxShape2D boxA, FixVector2 posA,
            BoxShape2D boxB, FixVector2 posB,
            out Contact2D contact)
        {
            contact = default;

            // 如果两个矩形都没有旋转，使用快速AABB检测
            if (boxA.Rotation == Fix64.Zero && boxB.Rotation == Fix64.Zero)
            {
                return BoxVsBoxAABB(boxA, posA, boxB, posB, out contact);
            }

            Fix64 halfWidthA = boxA.Width / Fix64.Two;
            Fix64 halfHeightA = boxA.Height / Fix64.Two;
            FixVector2[] localCornersA = new FixVector2[]
            {
                new FixVector2(-halfWidthA, -halfHeightA),
                new FixVector2(halfWidthA, -halfHeightA),
                new FixVector2(halfWidthA, halfHeightA),
                new FixVector2(-halfWidthA, halfHeightA)
            };

            Fix64 halfWidthB = boxB.Width / Fix64.Two;
            Fix64 halfHeightB = boxB.Height / Fix64.Two;
            FixVector2[] localCornersB = new FixVector2[]
            {
                new FixVector2(-halfWidthB, -halfHeightB),
                new FixVector2(halfWidthB, -halfHeightB),
                new FixVector2(halfWidthB, halfHeightB),
                new FixVector2(-halfWidthB, halfHeightB)
            };
            // 计算碰撞信息（从SAT结果中提取）
            // 使用MTV（最小平移向量）方法计算更准确的碰撞信息

            FixVector2[] cornersA = Utl.RotateAndTranslate(localCornersA, posA, boxA.Rotation);
            FixVector2[] cornersB = Utl.RotateAndTranslate(localCornersB, posB, boxB.Rotation);

            // 获取分离轴
            FixVector2[] axesA = GetRectAxes(boxA.Rotation);
            FixVector2[] axesB = GetRectAxes(boxB.Rotation);
            FixVector2[] allAxes = new FixVector2[axesA.Length + axesB.Length];
            System.Array.Copy(axesA, 0, allAxes, 0, axesA.Length);
            System.Array.Copy(axesB, 0, allAxes, axesA.Length, axesB.Length);

            // 找到最小重叠轴（MTV方向）
            Fix64 minOverlap = Fix64.MaxValue;
            FixVector2 mtvAxis = FixVector2.Zero;
            bool foundSeparatingAxis = false;

            foreach (FixVector2 axis in allAxes)
            {
                if (axis.x == Fix64.Zero && axis.y == Fix64.Zero)
                    continue;

                FixVector2 normalizedAxis = axis.Normalized();

                // 投影两个矩形
                Fix64 minA, maxA, minB, maxB;
                ProjectOntoAxis(cornersA, normalizedAxis, out minA, out maxA);
                ProjectOntoAxis(cornersB, normalizedAxis, out minB, out maxB);

                // 检查是否重叠
                if (maxA < minB || maxB < minA)
                {
                    // 找到分离轴
                    foundSeparatingAxis = true;
                    break;
                }

                // 计算重叠量
                Fix64 overlap = Fix64.Min(maxA, maxB) - Fix64.Max(minA, minB);
                if (overlap < minOverlap)
                {
                    minOverlap = overlap;
                    mtvAxis = normalizedAxis;
                }
            }

            if (foundSeparatingAxis || minOverlap == Fix64.MaxValue)
            {
                return false;
            }

            // 确定MTV方向（从A指向B）
            FixVector2 delta = posB - posA;

            // 确保法向量方向正确（从A指向B）
            if (FixVector2.Dot(mtvAxis, delta) < Fix64.Zero)
            {
                mtvAxis = -mtvAxis;
            }

            // 计算接触点（在重叠区域的中心）
            FixVector2 contactPoint = (posA + posB) / Fix64.Two;

            contact = new Contact2D
            {
                Point = contactPoint,
                Normal = mtvAxis,
                Penetration = minOverlap
            };

            return true;
        }


        /// <summary>
        /// 获取矩形的分离轴（法向量，垂直于边）
        /// 对于SAT，我们需要垂直于边的法向量作为分离轴
        /// </summary>
        private static FixVector2[] GetRectAxes(Fix64 rotation)
        {
            if (rotation == Fix64.Zero)
            {
                // 无旋转时，法向量是 (1,0) 和 (0,1)
                return new FixVector2[]
                {
                    new FixVector2(Fix64.One, Fix64.Zero),  // 右边缘的法向量（指向右）
                    new FixVector2(Fix64.Zero, Fix64.One)   // 上边缘的法向量（指向上）
                };
            }

            Fix64 cos = Fix64.Cos(rotation);
            Fix64 sin = Fix64.Sin(rotation);

            // 矩形的两条边的方向向量（旋转后）
            FixVector2 rightEdge = new FixVector2(cos, sin);      // 右边缘的方向
            FixVector2 upEdge = new FixVector2(-sin, cos);        // 上边缘的方向

            // 返回法向量（垂直于边）
            // 对于SAT，分离轴应该是垂直于边的法向量
            // 对于右边缘 (cos, sin)，法向量是逆时针旋转90度 = (-sin, cos) = upEdge
            // 对于上边缘 (-sin, cos)，法向量是逆时针旋转90度 = (-cos, -sin)
            // 但为了简化，我们使用边的方向作为分离轴（这在SAT中也是有效的）
            // 实际上，SAT可以使用边的方向或法向量方向，只要一致即可
            // 但MTV应该是法向量方向，所以我们需要返回法向量
            return new FixVector2[]
            {
                new FixVector2(-sin, cos),     // 右边缘的法向量（垂直于右边缘，等于upEdge）
                new FixVector2(-cos, -sin)     // 上边缘的法向量（垂直于上边缘）
            };
        }

        /// <summary>
        /// 将顶点投影到指定轴上
        /// </summary>
        private static void ProjectOntoAxis(FixVector2[] corners, FixVector2 axis, out Fix64 min, out Fix64 max)
        {
            min = Fix64.MaxValue;
            max = Fix64.MinValue;

            foreach (FixVector2 corner in corners)
            {
                Fix64 projection = FixVector2.Dot(corner, axis);
                min = Fix64.Min(min, projection);
                max = Fix64.Max(max, projection);
            }
        }

        /// <summary>
        /// 轴对齐矩形与矩形碰撞检测（快速版本）
        /// </summary>
        private static bool BoxVsBoxAABB(
            BoxShape2D boxA, FixVector2 posA,
            BoxShape2D boxB, FixVector2 posB,
            out Contact2D contact)
        {
            contact = default;

            Fix64 halfWidthA = boxA.Width / Fix64.Two;
            Fix64 halfHeightA = boxA.Height / Fix64.Two;
            Fix64 leftA = posA.x - halfWidthA;
            Fix64 rightA = posA.x + halfWidthA;
            Fix64 bottomA = posA.y - halfHeightA;
            Fix64 topA = posA.y + halfHeightA;

            Fix64 halfWidthB = boxB.Width / Fix64.Two;
            Fix64 halfHeightB = boxB.Height / Fix64.Two;
            Fix64 leftB = posB.x - halfWidthB;
            Fix64 rightB = posB.x + halfWidthB;
            Fix64 bottomB = posB.y - halfHeightB;
            Fix64 topB = posB.y + halfHeightB;

            if (rightA < leftB || leftA > rightB || topA < bottomB || bottomA > topB)
                return false;

            Fix64 overlapX = Fix64.Min(rightA, rightB) - Fix64.Max(leftA, leftB);
            Fix64 overlapY = Fix64.Min(topA, topB) - Fix64.Max(bottomA, bottomB);

            FixVector2 normal;
            Fix64 penetration;
            FixVector2 contactPoint;

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