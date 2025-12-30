using Frame.FixMath;

namespace Frame.Physics3D
{
    /// <summary>
    /// 碰撞检测器（实现各种形状之间的碰撞检测，3D）
    /// </summary>
    public static class CollisionDetector3D
    {
        /// <summary>
        /// 球体与球体碰撞检测
        /// </summary>
        public static bool SphereVsSphere(
            SphereShape3D sphereA, FixVector3 posA,
            SphereShape3D sphereB, FixVector3 posB,
            out Contact3D contact)
        {
            contact = default;

            // 计算球心距离
            FixVector3 delta = posB - posA;
            Fix64 distanceSquared = delta.SqrMagnitude();
            Fix64 radiusSum = sphereA.Radius + sphereB.Radius;
            Fix64 radiusSumSquared = radiusSum * radiusSum;

            // 如果距离平方大于半径和平方，则不碰撞
            if (distanceSquared > radiusSumSquared)
                return false;

            // 计算实际距离
            Fix64 distance = Fix64.Sqrt(distanceSquared);

            // 计算法向量（从A指向B）
            FixVector3 normal;
            if (distance > Fix64.Zero)
            {
                normal = delta.Normalized();
            }
            else
            {
                // 如果两个球完全重叠，使用默认方向
                normal = FixVector3.Right;
            }

            // 计算穿透深度
            Fix64 penetration = radiusSum - distance;

            // 计算接触点（在A的边界上）
            FixVector3 contactPoint = posA + normal * sphereA.Radius;

            contact = new Contact3D
            {
                Point = contactPoint,
                Normal = normal,
                Penetration = penetration
            };

            return true;
        }

        /// <summary>
        /// 球体与长方体碰撞检测（轴对齐）
        /// </summary>
        public static bool SphereVsBox(
            SphereShape3D sphere, FixVector3 spherePos,
            BoxShape3D box, FixVector3 boxPos,
            out Contact3D contact)
        {
            contact = default;

            Fix64 halfWidth = box.Width / Fix64.Two;
            Fix64 halfHeight = box.Height / Fix64.Two;
            Fix64 halfLength = box.Length / Fix64.Two;

            // 找到盒子上离球心最近的点
            Fix64 closestX = Fix64.Max(boxPos.x - halfWidth, Fix64.Min(spherePos.x, boxPos.x + halfWidth));
            Fix64 closestY = Fix64.Max(boxPos.y - halfHeight, Fix64.Min(spherePos.y, boxPos.y + halfHeight));
            Fix64 closestZ = Fix64.Max(boxPos.z - halfLength, Fix64.Min(spherePos.z, boxPos.z + halfLength));

            FixVector3 closestPoint = new FixVector3(closestX, closestY, closestZ);
            FixVector3 delta = closestPoint - spherePos;
            Fix64 distanceSquared = delta.SqrMagnitude();
            Fix64 radiusSquared = sphere.Radius * sphere.Radius;

            if (distanceSquared > radiusSquared)
                return false;

            Fix64 distance = Fix64.Sqrt(distanceSquared);
            FixVector3 normal;
            if (distance > Fix64.Zero)
            {
                normal = delta / distance;
            }
            else
            {
                // 球心在盒子内部，找到最短退出方向
                Fix64 distToLeft = spherePos.x - (boxPos.x - halfWidth);
                Fix64 distToRight = (boxPos.x + halfWidth) - spherePos.x;
                Fix64 distToBottom = spherePos.y - (boxPos.y - halfHeight);
                Fix64 distToTop = (boxPos.y + halfHeight) - spherePos.y;
                Fix64 distToBack = spherePos.z - (boxPos.z - halfLength);
                Fix64 distToFront = (boxPos.z + halfLength) - spherePos.z;

                Fix64 minDist = Fix64.Min(Fix64.Min(Fix64.Min(distToLeft, distToRight), Fix64.Min(distToBottom, distToTop)),
                    Fix64.Min(distToBack, distToFront));

                if (minDist == distToLeft)
                    normal = FixVector3.Right;
                else if (minDist == distToRight)
                    normal = FixVector3.Left;
                else if (minDist == distToBottom)
                    normal = FixVector3.Up;
                else if (minDist == distToTop)
                    normal = FixVector3.Down;
                else if (minDist == distToBack)
                    normal = FixVector3.Forward;
                else
                    normal = FixVector3.Back;
            }

            Fix64 penetration = sphere.Radius - distance;
            FixVector3 contactPoint = spherePos + normal * sphere.Radius;

            contact = new Contact3D
            {
                Point = contactPoint,
                Normal = normal,
                Penetration = penetration
            };

            return true;
        }

        /// <summary>
        /// 长方体与长方体碰撞检测（支持旋转，使用SAT算法）
        /// </summary>
        public static bool BoxVsBox(
            BoxShape3D boxA, FixVector3 posA,
            BoxShape3D boxB, FixVector3 posB,
            out Contact3D contact)
        {
            contact = default;

            // 如果两个Box都没有旋转，使用快速AABB检测
            if (boxA.Rotation == FixVector3.Zero && boxB.Rotation == FixVector3.Zero)
            {
                return BoxVsBoxAABB(boxA, posA, boxB, posB, out contact);
            }

            // 使用SAT（Separating Axis Theorem）算法检测OBB vs OBB
            return BoxVsBoxSAT(boxA, posA, boxB, posB, out contact);
        }

        /// <summary>
        /// 轴对齐Box碰撞检测（快速版本）
        /// </summary>
        private static bool BoxVsBoxAABB(
            BoxShape3D boxA, FixVector3 posA,
            BoxShape3D boxB, FixVector3 posB,
            out Contact3D contact)
        {
            contact = default;

            Fix64 halfWidthA = boxA.Width / Fix64.Two;
            Fix64 halfHeightA = boxA.Height / Fix64.Two;
            Fix64 halfLengthA = boxA.Length / Fix64.Two;

            Fix64 halfWidthB = boxB.Width / Fix64.Two;
            Fix64 halfHeightB = boxB.Height / Fix64.Two;
            Fix64 halfLengthB = boxB.Length / Fix64.Two;

            // AABB检测
            Fix64 leftA = posA.x - halfWidthA;
            Fix64 rightA = posA.x + halfWidthA;
            Fix64 bottomA = posA.y - halfHeightA;
            Fix64 topA = posA.y + halfHeightA;
            Fix64 backA = posA.z - halfLengthA;
            Fix64 frontA = posA.z + halfLengthA;

            Fix64 leftB = posB.x - halfWidthB;
            Fix64 rightB = posB.x + halfWidthB;
            Fix64 bottomB = posB.y - halfHeightB;
            Fix64 topB = posB.y + halfHeightB;
            Fix64 backB = posB.z - halfLengthB;
            Fix64 frontB = posB.z + halfLengthB;

            if (rightA < leftB || leftA > rightB ||
                topA < bottomB || bottomA > topB ||
                frontA < backB || backA > frontB)
                return false;

            // 计算重叠量
            Fix64 overlapX = Fix64.Min(rightA, rightB) - Fix64.Max(leftA, leftB);
            Fix64 overlapY = Fix64.Min(topA, topB) - Fix64.Max(bottomA, bottomB);
            Fix64 overlapZ = Fix64.Min(frontA, frontB) - Fix64.Max(backA, backB);

            // 找到最小重叠轴（MTV方向）
            FixVector3 normal;
            Fix64 penetration;
            FixVector3 contactPoint;

            if (overlapX < overlapY && overlapX < overlapZ)
            {
                penetration = overlapX;
                if (posA.x < posB.x)
                    normal = FixVector3.Right;
                else
                    normal = FixVector3.Left;

                Fix64 contactX = (Fix64.Max(leftA, leftB) + Fix64.Min(rightA, rightB)) / Fix64.Two;
                Fix64 contactY = (Fix64.Max(bottomA, bottomB) + Fix64.Min(topA, topB)) / Fix64.Two;
                Fix64 contactZ = (Fix64.Max(backA, backB) + Fix64.Min(frontA, frontB)) / Fix64.Two;
                contactPoint = new FixVector3(contactX, contactY, contactZ);
            }
            else if (overlapY < overlapZ)
            {
                penetration = overlapY;
                if (posA.y < posB.y)
                    normal = FixVector3.Up;
                else
                    normal = FixVector3.Down;

                Fix64 contactX = (Fix64.Max(leftA, leftB) + Fix64.Min(rightA, rightB)) / Fix64.Two;
                Fix64 contactY = (Fix64.Max(bottomA, bottomB) + Fix64.Min(topA, topB)) / Fix64.Two;
                Fix64 contactZ = (Fix64.Max(backA, backB) + Fix64.Min(frontA, frontB)) / Fix64.Two;
                contactPoint = new FixVector3(contactX, contactY, contactZ);
            }
            else
            {
                penetration = overlapZ;
                if (posA.z < posB.z)
                    normal = FixVector3.Forward;
                else
                    normal = FixVector3.Back;

                Fix64 contactX = (Fix64.Max(leftA, leftB) + Fix64.Min(rightA, rightB)) / Fix64.Two;
                Fix64 contactY = (Fix64.Max(bottomA, bottomB) + Fix64.Min(topA, topB)) / Fix64.Two;
                Fix64 contactZ = (Fix64.Max(backA, backB) + Fix64.Min(frontA, frontB)) / Fix64.Two;
                contactPoint = new FixVector3(contactX, contactY, contactZ);
            }

            contact = new Contact3D
            {
                Point = contactPoint,
                Normal = normal,
                Penetration = penetration
            };

            return true;
        }

        /// <summary>
        /// 使用SAT算法检测OBB vs OBB碰撞
        /// </summary>
        private static bool BoxVsBoxSAT(
            BoxShape3D boxA, FixVector3 posA,
            BoxShape3D boxB, FixVector3 posB,
            out Contact3D contact)
        {
            contact = default;

            // 获取两个Box的局部轴
            boxA.GetAxes(out FixVector3 axisAX, out FixVector3 axisAY, out FixVector3 axisAZ);
            boxB.GetAxes(out FixVector3 axisBX, out FixVector3 axisBY, out FixVector3 axisBZ);

            Fix64 halfWidthA = boxA.Width / Fix64.Two;
            Fix64 halfHeightA = boxA.Height / Fix64.Two;
            Fix64 halfLengthA = boxA.Length / Fix64.Two;

            Fix64 halfWidthB = boxB.Width / Fix64.Two;
            Fix64 halfHeightB = boxB.Height / Fix64.Two;
            Fix64 halfLengthB = boxB.Length / Fix64.Two;

            // 计算相对位置
            FixVector3 delta = posB - posA;

            // 测试15个分离轴（6个面法线 + 9个边叉积）
            Fix64 minOverlap = Fix64.MaxValue;
            FixVector3 mtvAxis = FixVector3.Zero;
            bool foundSeparatingAxis = false;

            // 测试BoxA的3个轴
            FixVector3[] axesA = { axisAX, axisAY, axisAZ };
            // 测试BoxB的3个轴
            FixVector3[] axesB = { axisBX, axisBY, axisBZ };
            Fix64[] halfSizesA = { halfWidthA, halfHeightA, halfLengthA };
            Fix64[] halfSizesB = { halfWidthB, halfHeightB, halfLengthB };

            for (int i = 0; i < 3; i++)
            {
                FixVector3 axis = axesA[i];
                if (!TestAxis(axis, delta, axisAX, axisAY, axisAZ, halfSizesA,
                    axisBX, axisBY, axisBZ, halfSizesB,
                    ref minOverlap, ref mtvAxis, ref foundSeparatingAxis))
                {
                    return false;
                }
            }

          
            for (int i = 0; i < 3; i++)
            {
                FixVector3 axis = axesB[i];
                if (!TestAxis(axis, delta, axisAX, axisAY, axisAZ, halfSizesA,
                    axisBX, axisBY, axisBZ, halfSizesB,
                    ref minOverlap, ref mtvAxis, ref foundSeparatingAxis))
                {
                    return false;
                }
            }

            // 测试9个边叉积（简化：只测试最重要的几个）
            FixVector3[] crossAxes = {
                FixVector3.Cross(axisAX, axisBX),
                FixVector3.Cross(axisAX, axisBY),
                FixVector3.Cross(axisAX, axisBZ),
                FixVector3.Cross(axisAY, axisBX),
                FixVector3.Cross(axisAY, axisBY),
                FixVector3.Cross(axisAY, axisBZ),
                FixVector3.Cross(axisAZ, axisBX),
                FixVector3.Cross(axisAZ, axisBY),
                FixVector3.Cross(axisAZ, axisBZ)
            };

            foreach (var axis in crossAxes)
            {
                Fix64 sqrLength = axis.SqrMagnitude();
                if (sqrLength < (Fix64)0.0001m) continue; // 轴太短，跳过

                FixVector3 normalizedAxis = axis / Fix64.Sqrt(sqrLength);
                if (!TestAxis(normalizedAxis, delta, axisAX, axisAY, axisAZ, halfSizesA,
                    axisBX, axisBY, axisBZ, halfSizesB,
                    ref minOverlap, ref mtvAxis, ref foundSeparatingAxis))
                {
                    return false;
                }
            }

            if (foundSeparatingAxis || minOverlap == Fix64.MaxValue)
            {
                return false;
            }

            // 确定MTV方向（从A指向B）
            if (FixVector3.Dot(mtvAxis, delta) < Fix64.Zero)
            {
                mtvAxis = -mtvAxis;
            }

            // 计算接触点（简化：使用中心点）
            FixVector3 contactPoint = (posA + posB) / Fix64.Two;

            contact = new Contact3D
            {
                Point = contactPoint,
                Normal = mtvAxis,
                Penetration = minOverlap
            };

            return true;
        }

        /// <summary>
        /// 测试单个分离轴
        /// </summary>
        private static bool TestAxis(
            FixVector3 axis, FixVector3 delta,
            FixVector3 axisAX, FixVector3 axisAY, FixVector3 axisAZ, Fix64[] halfSizesA,
            FixVector3 axisBX, FixVector3 axisBY, FixVector3 axisBZ, Fix64[] halfSizesB,
            ref Fix64 minOverlap, ref FixVector3 mtvAxis, ref bool foundSeparatingAxis)
        {
            // 计算两个Box在轴上的投影半径
            Fix64 radiusA = Fix64.Abs(FixVector3.Dot(axisAX, axis)) * halfSizesA[0] +
                           Fix64.Abs(FixVector3.Dot(axisAY, axis)) * halfSizesA[1] +
                           Fix64.Abs(FixVector3.Dot(axisAZ, axis)) * halfSizesA[2];

            Fix64 radiusB = Fix64.Abs(FixVector3.Dot(axisBX, axis)) * halfSizesB[0] +
                           Fix64.Abs(FixVector3.Dot(axisBY, axis)) * halfSizesB[1] +
                           Fix64.Abs(FixVector3.Dot(axisBZ, axis)) * halfSizesB[2];

            // 计算中心点在轴上的投影距离
            Fix64 distance = Fix64.Abs(FixVector3.Dot(delta, axis));

            // 检查是否分离
            if (distance > radiusA + radiusB)
            {
                foundSeparatingAxis = true;
                return false;
            }

            // 计算重叠量
            Fix64 overlap = radiusA + radiusB - distance;
            if (overlap < minOverlap)
            {
                minOverlap = overlap;
                mtvAxis = axis;
            }

            return true;
        }
    }
}

