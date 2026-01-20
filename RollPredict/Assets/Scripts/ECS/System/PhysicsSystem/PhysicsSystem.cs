using System.Collections.Generic;
using System.Linq;
using Frame.FixMath;
using Frame.Physics2D;
using Proto;

namespace Frame.ECS
{
    public enum PhysicsLayer : int
    {
        Player = 1,
        Bullet = 2,
        Zombie = 3,
        Wall = 4,
    }

    /// <summary>
    /// 物理系统：处理物理模拟和碰撞检测
    /// </summary>
    public class PhysicsSystem : ISystem
    {
        // 物理世界配置
        public FixVector2 gravity = FixVector2.Down;
        public const int iterations = 6; // 碰撞分离迭代次数
        public const int subSteps = 2; // 子步迭代次数

        // 碰撞矩阵（Layer -> Layer -> 是否忽略）
        private Dictionary<(int, int), bool> collisionMatrix = new Dictionary<(int, int), bool>();

        // 四叉树（用于宽相位碰撞检测）
        private QuadTreeECS quadTree = new QuadTreeECS();

        // 力累加器（Entity ID -> 力向量）
        private Dictionary<Entity, FixVector2> forceAccumulator = new Dictionary<Entity, FixVector2>();


        public void Execute(World world, List<FrameData> inputs)
        {
            Fix64 deltaTime = Fix64.One;
            Fix64 subStepDeltaTime = deltaTime / (Fix64)subSteps;

            ClearCollisionInfo(world);

            for (int subStep = 0; subStep < subSteps; subStep++)
            {
                UpdateSingleStep(world, subStepDeltaTime);
            }
        }

        private void UpdateSingleStep(World world, Fix64 deltaTime)
        {
            // 1. 收集力（重力等）
            CollectForces(world);

            // 2. 更新位置和速度
            UpdatePositions(world, deltaTime);

            // 3. 更新四叉树
            UpdateQuadTree(world);

            // 4. 碰撞检测和响应（迭代多次）
            for (int i = 0; i < iterations; i++)
            {
                ResolveCollisions(world);
            }

            // 5. 清除力累加器
            ClearForces();
        }


        /// <summary>
        /// 收集所有力（重力、用户施加的力等）
        /// </summary>
        private void CollectForces(World world)
        {
            foreach (var (entity, body) in world.GetEntitiesWithComponents<PhysicsBodyComponent>())
            {
                if (body.IsDynamic && body.useGravity)
                {
                    // 重力 = 质量 * 重力加速度
                    FixVector2 gravityForce = gravity * body.mass;
                    ApplyForce(entity, gravityForce);
                }
            }
        }

        /// <summary>
        /// 应用力（累积到力累加器中）
        /// </summary>
        public void ApplyForce(Entity entity, FixVector2 force)
        {
            if (!forceAccumulator.ContainsKey(entity))
                forceAccumulator[entity] = FixVector2.Zero;
            forceAccumulator[entity] += force;
        }

        /// <summary>
        /// 清除力累加器
        /// </summary>
        private void ClearForces()
        {
            forceAccumulator.Clear();
        }

        /// <summary>
        /// 更新位置和速度
        /// </summary>
        private void UpdatePositions(World world, Fix64 deltaTime)
        {
            foreach (var (entity, body, transform, velocity) in world
                         .GetEntitiesWithComponents<PhysicsBodyComponent, Transform2DComponent, VelocityComponent>())
            {
                if (!body.IsDynamic)
                    continue;


                // 计算加速度：a = F/m
                FixVector2 acceleration = FixVector2.Zero;
                if (forceAccumulator.TryGetValue(entity, out var force))
                {
                    acceleration = force / body.mass;
                }

                // 更新速度：v = v + a*dt
                var newVelocity = velocity;
                newVelocity.velocity += acceleration * deltaTime;

                // 更新位置：x = x + v * dt
                var newTransform = transform;
                newTransform.position += velocity.velocity * deltaTime;

                // 应用线性阻尼
                if (body.linearDamping > Fix64.Zero)
                {
                    Fix64 dampingFactor =
                        Fix64.One - Fix64.Clamp(body.linearDamping * deltaTime, Fix64.Zero, Fix64.One);
                    newVelocity.velocity *= dampingFactor;
                }

                // 更新Component
                world.AddComponent(entity, newTransform);
                world.AddComponent(entity, newVelocity);
            }
        }

        /// <summary>
        /// 更新四叉树
        /// </summary>
        private void UpdateQuadTree(World world)
        {
            // 检查是否需要扩容
            var allBounds = new List<(Entity entity, FixRect bounds)>();
            FixRect quadTreeBound = new FixRect(Fix64.Zero, Fix64.Zero, Fix64.Zero, Fix64.Zero);
            foreach (var (entity, transform, shape) in world
                         .GetEntitiesWithComponents<Transform2DComponent, CollisionShapeComponent>())
            {
                FixRect bounds = shape.GetBounds(transform.position);
                allBounds.Add((entity, bounds));
                quadTreeBound.Union(bounds);
            }

            quadTree.Init(quadTreeBound);
            foreach (var (entity, bounds) in allBounds)
            {
                quadTree.Add(entity, bounds);
            }
        }

        /// <summary>
        /// 碰撞检测和响应
        /// </summary>
        private void ResolveCollisions(World world)
        {
            var checkedPairs = new HashSet<(Entity, Entity)>();


            foreach (var (entityA, bodyA, transformA, shapeA) in world
                         .GetEntitiesWithComponents<PhysicsBodyComponent, Transform2DComponent,
                             CollisionShapeComponent>())
            {
                // 只处理动态物体
                if (bodyA.isStatic)
                    continue;

                // 宽相位：使用四叉树查询
                FixRect aabbA = shapeA.GetBounds(transformA.position);
                var candidateIds = quadTree.Query(aabbA);

                foreach (Entity entityB in candidateIds)
                {
                    if (!entityB.IsValid || entityB.Equals(entityA))
                        continue;

                    // 检查碰撞对是否已检测
                    var pair = (entityA.Id < entityB.Id) ? (entityA, entityB) : (entityB, entityA);
                    if (checkedPairs.Contains(pair))
                        continue;
                    checkedPairs.Add(pair);

                    if (!world.TryGetComponent<PhysicsBodyComponent>(entityB, out var bodyB))
                        continue;

                    // 检查Layer碰撞矩阵
                    if (ShouldIgnoreCollision(bodyA.layer, bodyB.layer))
                        continue;

                    // 窄相位：精确碰撞检测
                    if (!world.TryGetComponent<Transform2DComponent>(entityB, out var transformB))
                        continue;

                    if (!world.TryGetComponent<CollisionShapeComponent>(entityB, out var shapeB))
                        continue;

                    if (CollisionDetectorECS.CheckCollision(
                            shapeA, transformA.position,
                            shapeB, transformB.position,
                            out Contact2D contact))
                    {
                        // 记录碰撞信息
                        RecordCollision(world, entityA, entityB);

                        // 处理碰撞响应
                        ResolveContact(world, entityA, entityB, contact);
                    }
                }
            }
        }

        /// <summary>
        /// 记录碰撞信息到CollisionComponent
        /// </summary>
        private void RecordCollision(World world, Entity entityA, Entity entityB)
        {
            // 确保entityA有CollisionComponent
            if (!world.TryGetComponent<CollisionComponent>(entityA, out var collisionA))
            {
                collisionA = new CollisionComponent();
                world.AddComponent(entityA, collisionA);
            }

            // 确保entityB有CollisionComponent
            if (!world.TryGetComponent<CollisionComponent>(entityB, out var collisionB))
            {
                collisionB = new CollisionComponent();
                world.AddComponent(entityB, collisionB);
            }

            // 双向记录
            collisionA.AddCollidingEntity(entityB.Id);
            collisionB.AddCollidingEntity(entityA.Id);

            world.AddComponent(entityA, collisionA);
            world.AddComponent(entityB, collisionB);
        }

        /// <summary>
        /// 清空所有Entity的碰撞信息（每帧开始时调用）
        /// </summary>
        private void ClearCollisionInfo(World world)
        {
            foreach (var (entity, _) in world.GetEntitiesWithComponents<PhysicsBodyComponent>())
            {
                if (world.TryGetComponent<CollisionComponent>(entity, out var collision))
                {
                    collision.Clear();
                    world.AddComponent(entity, collision);
                }
            }
        }

        /// <summary>
        /// 处理碰撞响应（分离物体并修正速度）
        /// </summary>
        private void ResolveContact(World world, Entity entityA, Entity entityB, Contact2D contact)
        {
            if (!world.TryGetComponent<PhysicsBodyComponent>(entityA, out var bodyA))
                return;
            if (!world.TryGetComponent<PhysicsBodyComponent>(entityB, out var bodyB))
                return;

            // 如果至少有一个是触发器，只记录碰撞，不进行物理响应
            if (bodyA.isTrigger || bodyB.isTrigger)
                return;

            // 如果两个都是静态物体，不需要分离
            if (!bodyA.IsDynamic && !bodyB.IsDynamic)
                return;

            // 1. 分离重叠的物体
            SeparateBodies(world, entityA, entityB, contact, bodyA, bodyB);

            // 2. 修正速度（碰撞响应）
            ResolveVelocity(world, entityA, entityB, contact, bodyA, bodyB);
        }

        /// <summary>
        /// 分离重叠的物体
        /// </summary>
        private void SeparateBodies(World world, Entity entityA, Entity entityB, Contact2D contact,
            PhysicsBodyComponent bodyA, PhysicsBodyComponent bodyB)
        {
            if (!world.TryGetComponent<Transform2DComponent>(entityA, out var transformA))
                return;
            if (!world.TryGetComponent<Transform2DComponent>(entityB, out var transformB))
                return;

            // 计算需要移动的距离（根据质量分配）
            Fix64 totalMass = bodyA.mass + bodyB.mass;
            Fix64 moveA = bodyB.mass / totalMass;
            Fix64 moveB = bodyA.mass / totalMass;

            // 如果某个物体是静态的，只移动另一个
            if (!bodyA.IsDynamic)
            {
                moveA = Fix64.Zero;
                moveB = Fix64.One;
            }
            else if (!bodyB.IsDynamic)
            {
                moveA = Fix64.One;
                moveB = Fix64.Zero;
            }

            // 分离向量
            FixVector2 separation = contact.Normal * contact.Penetration;

            // 移动物体
            transformA.position -= separation * moveA;
            transformB.position += separation * moveB;

            world.AddComponent(entityA, transformA);
            world.AddComponent(entityB, transformB);
        }

        /// <summary>
        /// 修正速度（碰撞响应）
        /// </summary>
        private void ResolveVelocity(World world, Entity entityA, Entity entityB, Contact2D contact,
            PhysicsBodyComponent bodyA, PhysicsBodyComponent bodyB)
        {
            if (!world.TryGetComponent<VelocityComponent>(entityA, out var velocityA))
                return;
            if (!world.TryGetComponent<VelocityComponent>(entityB, out var velocityB))
                return;

            // 计算相对速度
            FixVector2 relativeVelocity = velocityB.velocity - velocityA.velocity;

            // 计算沿法向量方向的相对速度
            Fix64 velocityAlongNormal = FixVector2.Dot(relativeVelocity, contact.Normal);

            // 如果物体正在分离，不需要处理
            if (velocityAlongNormal > Fix64.Zero)
                return;

            // 计算恢复系数（弹性）
            Fix64 restitution = Fix64.Min(bodyA.restitution, bodyB.restitution);

            // 计算冲量大小
            Fix64 invMassA = bodyA.IsDynamic ? Fix64.One / bodyA.mass : Fix64.Zero;
            Fix64 invMassB = bodyB.IsDynamic ? Fix64.One / bodyB.mass : Fix64.Zero;
            Fix64 invMassSum = invMassA + invMassB;

            if (invMassSum == Fix64.Zero)
                return;

            Fix64 impulseMagnitude = -(Fix64.One + restitution) * velocityAlongNormal / invMassSum;

            // 应用冲量
            FixVector2 impulse = contact.Normal * impulseMagnitude;
            velocityA.velocity -= impulse * invMassA;
            velocityB.velocity += impulse * invMassB;

            // 处理摩擦力
            FixVector2 tangent = relativeVelocity - contact.Normal * velocityAlongNormal;
            Fix64 tangentLength = tangent.Magnitude();
            if (tangentLength > Fix64.Zero)
            {
                FixVector2 tangentDir = tangent / tangentLength;
                Fix64 tangentVelocity = FixVector2.Dot(relativeVelocity, tangentDir);
                Fix64 frictionImpulse = -tangentVelocity / invMassSum;

                Fix64 frictionCoeff = Fix64.Sqrt(bodyA.friction * bodyB.friction);
                Fix64 maxFriction = Fix64.Abs(impulseMagnitude) * frictionCoeff;
                frictionImpulse = Fix64.Clamp(frictionImpulse, -maxFriction, maxFriction);

                FixVector2 friction = tangentDir * frictionImpulse;
                velocityA.velocity -= friction * invMassA;
                velocityB.velocity += friction * invMassB;
            }

            world.AddComponent(entityA, velocityA);
            world.AddComponent(entityB, velocityB);
        }

        /// <summary>
        /// 检查两个Layer之间是否应该忽略碰撞
        /// </summary>
        private bool ShouldIgnoreCollision(int layerA, int layerB)
        {
            if (layerA == 0 && layerB == 0)
                return false;

            var key1 = (layerA, layerB);
            var key2 = (layerB, layerA);

            if (collisionMatrix.TryGetValue(key1, out bool ignore1))
                return ignore1;

            if (collisionMatrix.TryGetValue(key2, out bool ignore2))
                return ignore2;

            return false;
        }

        /// <summary>
        /// 忽略两个Layer之间的碰撞
        /// </summary>
        public void IgnoreLayerCollision(int layerA, int layerB)
        {
            if (layerA == 0 && layerB == 0)
                return;

            var key1 = (layerA, layerB);
            var key2 = (layerB, layerA);

            collisionMatrix[key1] = true;
            collisionMatrix[key2] = true;
        }

        /// <summary>
        /// 恢复两个Layer之间的碰撞
        /// </summary>
        public void ResumeLayerCollision(int layerA, int layerB)
        {
            var key1 = (layerA, layerB);
            var key2 = (layerB, layerA);

            collisionMatrix.Remove(key1);
            collisionMatrix.Remove(key2);
        }

        /// <summary>
        /// 查询指定圆形区域内的Entity（通过QuadTree）
        /// 
        /// 使用场景：
        /// - 僵尸攻击范围检测
        /// - 技能范围检测
        /// - 区域查询
        /// 
        /// 性能优化：
        /// - 使用QuadTree宽相位检测，只对候选Entity进行精确圆形检测
        /// - 支持Layer筛选，减少不必要的检测
        /// </summary>
        /// <param name="world">World实例（用于获取Entity组件）</param>
        /// <param name="center">圆心位置</param>
        /// <param name="radius">半径</param>
        /// <param name="layerMask">层掩码（只返回匹配的层，-1表示所有层）</param>
        /// <returns>匹配的Entity列表</returns>
        public List<Entity> QueryCircleRegion(World world, FixVector2 center, Fix64 radius, int layerMask = -1)
        {
            // 将圆形转换为AABB（用于QuadTree查询）
            // AABB = 圆的外接矩形
            FixRect aabb = new FixRect(
                center.x - radius, // x (左下角)
                center.y - radius, // y (左下角)
                radius * Fix64.Two, // width
                radius * Fix64.Two // height
            );

            // 查询AABB区域内的所有Entity（宽相位）
            var candidates = quadTree.Query(aabb);

            // 精确圆形检测 + Layer过滤（窄相位）
            var result = new List<Entity>();
            Fix64 radiusSqr = radius * radius;

            foreach (var entity in candidates)
            {
                // 1. Layer筛选（如果指定了layerMask）
                if (layerMask >= 0)
                {
                    if (!world.TryGetComponent<PhysicsBodyComponent>(entity, out var body))
                        continue;
                    if (body.layer != layerMask)
                        continue;
                }

                // 2. 精确圆形检测
                if (!world.TryGetComponent<Transform2DComponent>(entity, out var transform))
                    continue;

                FixVector2 diff = transform.position - center;
                Fix64 distanceSqr = diff.x * diff.x + diff.y * diff.y;

                if (distanceSqr <= radiusSqr)
                {
                    result.Add(entity);
                }
            }

            return result;
        }

        /// <summary>
        /// 查询指定旋转矩形区域内的Entity（通过QuadTree）
        /// 
        /// 使用场景：
        /// - 僵尸攻击伤害判定（旋转矩形）
        /// - 技能范围检测（矩形区域）
        /// - 区域查询
        /// 
        /// 性能优化：
        /// - 使用QuadTree宽相位检测，只对候选Entity进行精确旋转矩形检测
        /// - 支持Layer筛选，减少不必要的检测
        /// </summary>
        /// <param name="world">World实例（用于获取Entity组件）</param>
        /// <param name="center">矩形中心位置</param>
        /// <param name="size">矩形尺寸（宽度、高度）</param>
        /// <param name="rotation">旋转角度（弧度，0表示轴对齐）</param>
        /// <param name="layerMask">层掩码（只返回匹配的层，-1表示所有层）</param>
        /// <returns>匹配的Entity列表</returns>
        public List<Entity> QueryRotatedRectRegion(World world, FixVector2 center, FixVector2 size, Fix64 rotation, int layerMask = -1)
        {
            // 1. 创建旋转矩形形状（用于精确碰撞检测）
            var queryBox = new BoxShape2D(size.x, size.y, rotation);
            
            // 2. 计算旋转矩形的AABB（用于QuadTree查询，宽相位）
            FixRect queryAABB = queryBox.GetBounds(center);
            
            // 3. 使用QuadTree查询AABB范围内的所有Entity（宽相位）
            var candidates = quadTree.Query(queryAABB);
            
            // 4. 精确旋转矩形检测 + Layer过滤（窄相位）
            var result = new List<Entity>();
            
            foreach (var entity in candidates)
            {
                // 1. Layer筛选（如果指定了layerMask）
                if (layerMask >= 0)
                {
                    if (!world.TryGetComponent<PhysicsBodyComponent>(entity, out var body))
                        continue;
                    if (body.layer != layerMask)
                        continue;
                }
                
                // 2. 获取Entity的Transform和CollisionShape
                if (!world.TryGetComponent<Transform2DComponent>(entity, out var transform))
                    continue;
                if (!world.TryGetComponent<CollisionShapeComponent>(entity, out var shape))
                    continue;
                
                // 3. 将ECS的CollisionShapeComponent转换为Physics2D的CollisionShape2D
                CollisionShape2D entityShape;
                if (shape.shapeType == ShapeType.Circle)
                {
                    entityShape = new CircleShape2D(shape.radius);
                }
                else if (shape.shapeType == ShapeType.Box)
                {
                    // ECS的Box不支持旋转，所以rotation为0
                    entityShape = new BoxShape2D(shape.size.x, shape.size.y, Fix64.Zero);
                }
                else
                {
                    continue; // 未知形状类型
                }
                
                // 4. 精确碰撞检测：查询矩形 vs Entity形状
                if (CollisionShape2D.CheckCollision(
                        queryBox, center,
                        entityShape, transform.position,
                        out Contact2D contact))
                {
                    result.Add(entity);
                }
            }
            
            return result;
        }

        /// <summary>
        /// 射线检测：从起点沿方向发射射线，检测第一个碰撞的Entity
        /// 
        /// 使用场景：
        /// - 枪械射击检测
        /// - 视线检测（无限距离）
        /// - 激光武器
        /// 
        /// 性能优化：
        /// - 使用QuadTree宽相位检测，只对射线路径上的Entity进行精确检测
        /// - 支持Layer筛选
        /// - 支持最大距离限制
        /// </summary>
        /// <param name="world">World实例</param>
        /// <param name="origin">射线起点</param>
        /// <param name="direction">射线方向（必须归一化）</param>
        /// <param name="maxDistance">最大检测距离（0表示无限）</param>
        /// <param name="layerMask">层掩码（只检测匹配的层，-1表示所有层）</param>
        /// <param name="hitInfo">碰撞信息（输出）</param>
        /// <returns>true=检测到碰撞，false=未检测到</returns>
        public bool Raycast(World world, FixVector2 origin, FixVector2 direction, Fix64 maxDistance, int layerMask, out RaycastHit2D hitInfo)
        {
            hitInfo = new RaycastHit2D();
            
            // 计算射线的AABB（用于QuadTree查询）
            FixVector2 endPoint = maxDistance > Fix64.Zero 
                ? origin + direction * maxDistance 
                : origin + direction * (Fix64)1000; // 无限距离，使用很大的值
            
            Fix64 minX = Fix64.Min(origin.x, endPoint.x);
            Fix64 maxX = Fix64.Max(origin.x, endPoint.x);
            Fix64 minY = Fix64.Min(origin.y, endPoint.y);
            Fix64 maxY = Fix64.Max(origin.y, endPoint.y);
            
            // 扩展AABB，确保能够覆盖所有可能的碰撞体
            Fix64 padding = (Fix64)2; // 2个单位的padding
            FixRect rayAABB = new FixRect(
                minX - padding,
                minY - padding,
                (maxX - minX) + padding * Fix64.Two,
                (maxY - minY) + padding * Fix64.Two
            );
            
            // 宽相位：使用QuadTree查询
            var candidates = quadTree.Query(rayAABB);
            
            // 窄相位：精确射线检测
            Fix64 minDistance = Fix64.MaxValue;
            Entity closestEntity = Entity.Invalid;
            FixVector2 closestPoint = FixVector2.Zero;
            FixVector2 closestNormal = FixVector2.Zero;
            
            foreach (var entity in candidates)
            {
                // Layer筛选
                if (layerMask >= 0)
                {
                    if (!world.TryGetComponent<PhysicsBodyComponent>(entity, out var body))
                        continue;
                    if (body.layer != layerMask)
                        continue;
                }
                
                // 获取Entity的Transform和CollisionShape
                if (!world.TryGetComponent<Transform2DComponent>(entity, out var transform))
                    continue;
                if (!world.TryGetComponent<CollisionShapeComponent>(entity, out var shape))
                    continue;
                
                // 射线与形状相交检测
                if (RaycastShape(origin, direction, maxDistance, transform.position, shape, 
                    out Fix64 distance, out FixVector2 hitPoint, out FixVector2 hitNormal))
                {
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestEntity = entity;
                        closestPoint = hitPoint;
                        closestNormal = hitNormal;
                    }
                }
            }
            
            // 如果找到碰撞
            if (closestEntity.IsValid)
            {
                hitInfo = new RaycastHit2D
                {
                    entity = closestEntity,
                    point = closestPoint,
                    normal = closestNormal,
                    distance = minDistance
                };
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 线段检测：检测从起点到终点的线段是否与任何Entity碰撞
        /// 
        /// 使用场景：
        /// - 视线检测（有限距离）⭐
        /// - 近战武器挥舞检测
        /// - 绳索/链条碰撞
        /// 
        /// 与Raycast的区别：
        /// - Raycast：无限长的射线（或指定最大距离）
        /// - Linecast：有限长的线段（明确的起点和终点）
        /// </summary>
        /// <param name="world">World实例</param>
        /// <param name="start">线段起点</param>
        /// <param name="end">线段终点</param>
        /// <param name="layerMask">层掩码（只检测匹配的层，-1表示所有层）</param>
        /// <param name="hitInfo">碰撞信息（输出）</param>
        /// <returns>true=检测到碰撞，false=未检测到</returns>
        public bool Linecast(World world, FixVector2 start, FixVector2 end, int layerMask, out RaycastHit2D hitInfo)
        {
            FixVector2 direction = end - start;
            Fix64 distance = direction.Magnitude();
            
            if (distance == Fix64.Zero)
            {
                hitInfo = new RaycastHit2D();
                return false;
            }

            direction = direction.Normalized();
            
            return Raycast(world, start, direction, distance, layerMask, out hitInfo);
        }
        
        /// <summary>
        /// 射线与形状相交检测（内部方法）
        /// </summary>
        private bool RaycastShape(FixVector2 rayOrigin, FixVector2 rayDirection, Fix64 maxDistance,
            FixVector2 shapePosition, CollisionShapeComponent shape,
            out Fix64 distance, out FixVector2 hitPoint, out FixVector2 hitNormal)
        {
            distance = Fix64.Zero;
            hitPoint = FixVector2.Zero;
            hitNormal = FixVector2.Zero;
            
            if (shape.shapeType == ShapeType.Circle)
            {
                return RaycastCircle(rayOrigin, rayDirection, maxDistance, shapePosition, shape.radius,
                    out distance, out hitPoint, out hitNormal);
            }
            else if (shape.shapeType == ShapeType.Box)
            {
                return RaycastBox(rayOrigin, rayDirection, maxDistance, shapePosition, shape.size.x/(Fix64)2, shape.size.y/(Fix64)2,
                    out distance, out hitPoint, out hitNormal);
            }
            
            return false;
        }
        
        /// <summary>
        /// 射线与圆形相交检测
        /// 
        /// 算法：
        /// 1. 计算射线原点到圆心的向量
        /// 2. 计算投影长度（射线方向上的距离）
        /// 3. 计算垂直距离（判断是否相交）
        /// 4. 计算交点
        /// </summary>
        private bool RaycastCircle(FixVector2 rayOrigin, FixVector2 rayDirection, Fix64 maxDistance,
            FixVector2 circleCenter, Fix64 circleRadius,
            out Fix64 distance, out FixVector2 hitPoint, out FixVector2 hitNormal)
        {
            distance = Fix64.Zero;
            hitPoint = FixVector2.Zero;
            hitNormal = FixVector2.Zero;
            
            // 计算射线原点到圆心的向量
            FixVector2 toCircle = circleCenter - rayOrigin;
            
            // 计算投影长度（射线方向上的距离）
            Fix64 projection = toCircle.x * rayDirection.x + toCircle.y * rayDirection.y;
            
            // 如果投影为负，说明圆在射线背后
            if (projection < Fix64.Zero)
            {
                return false;
            }
            
            // 计算最近点
            FixVector2 closestPoint = rayOrigin + rayDirection * projection;
            
            // 计算垂直距离的平方
            FixVector2 diff = circleCenter - closestPoint;
            Fix64 distSqr = diff.x * diff.x + diff.y * diff.y;
            Fix64 radiusSqr = circleRadius * circleRadius;
            
            // 如果垂直距离大于半径，不相交
            if (distSqr > radiusSqr)
            {
                return false;
            }
            
            // 计算交点距离
            Fix64 halfChord = Fix64.Sqrt(radiusSqr - distSqr);
            distance = projection - halfChord;
            
            // 如果距离为负，说明射线起点在圆内
            if (distance < Fix64.Zero)
            {
                distance = Fix64.Zero;
            }
            
            // 检查最大距离限制
            if (maxDistance > Fix64.Zero && distance > maxDistance)
            {
                return false;
            }
            
            // 计算交点和法线
            hitPoint = rayOrigin + rayDirection * distance;
            hitNormal = (hitPoint - circleCenter) / circleRadius; // 归一化法线
            
            return true;
        }
        
        /// <summary>
        /// 射线与矩形相交检测（AABB）
        /// 
        /// 算法：使用Slab方法（分别检测X和Y轴）
        /// </summary>
        private bool RaycastBox(FixVector2 rayOrigin, FixVector2 rayDirection, Fix64 maxDistance,
            FixVector2 boxCenter, Fix64 halfWidth, Fix64 halfHeight,
            out Fix64 distance, out FixVector2 hitPoint, out FixVector2 hitNormal)
        {
            distance = Fix64.Zero;
            hitPoint = FixVector2.Zero;
            hitNormal = FixVector2.Zero;
            
            // 计算AABB边界
            Fix64 minX = boxCenter.x - halfWidth;
            Fix64 maxX = boxCenter.x + halfWidth;
            Fix64 minY = boxCenter.y - halfHeight;
            Fix64 maxY = boxCenter.y + halfHeight;
            
            // Slab方法：分别计算X和Y轴的进入/退出时间
            Fix64 tMin = Fix64.MinValue;
            Fix64 tMax = Fix64.MaxValue;
            
            // X轴
            if (Fix64.Abs(rayDirection.x) > Fix64.Zero)
            {
                Fix64 t1 = (minX - rayOrigin.x) / rayDirection.x;
                Fix64 t2 = (maxX - rayOrigin.x) / rayDirection.x;
                
                Fix64 tMinX = Fix64.Min(t1, t2);
                Fix64 tMaxX = Fix64.Max(t1, t2);
                
                tMin = Fix64.Max(tMin, tMinX);
                tMax = Fix64.Min(tMax, tMaxX);
            }
            else
            {
                // 射线平行于X轴
                if (rayOrigin.x < minX || rayOrigin.x > maxX)
                {
                    return false;
                }
            }
            
            // Y轴
            if (Fix64.Abs(rayDirection.y) > Fix64.Zero)
            {
                Fix64 t1 = (minY - rayOrigin.y) / rayDirection.y;
                Fix64 t2 = (maxY - rayOrigin.y) / rayDirection.y;
                
                Fix64 tMinY = Fix64.Min(t1, t2);
                Fix64 tMaxY = Fix64.Max(t1, t2);
                
                tMin = Fix64.Max(tMin, tMinY);
                tMax = Fix64.Min(tMax, tMaxY);
            }
            else
            {
                // 射线平行于Y轴
                if (rayOrigin.y < minY || rayOrigin.y > maxY)
                {
                    return false;
                }
            }
            
            // 检查是否相交
            if (tMax < tMin || tMin < Fix64.Zero)
            {
                return false;
            }
            
            distance = tMin;
            
            // 检查最大距离限制
            if (maxDistance > Fix64.Zero && distance > maxDistance)
            {
                return false;
            }
            
            // 计算交点
            hitPoint = rayOrigin + rayDirection * distance;
            
            // 计算法线（根据交点在哪个面上）
            Fix64 epsilon = (Fix64)0.01f;
            if (Fix64.Abs(hitPoint.x - minX) < epsilon)
            {
                hitNormal = FixVector2.Left; // 左侧面
            }
            else if (Fix64.Abs(hitPoint.x - maxX) < epsilon)
            {
                hitNormal = FixVector2.Right; // 右侧面
            }
            else if (Fix64.Abs(hitPoint.y - minY) < epsilon)
            {
                hitNormal = FixVector2.Down; // 下侧面
            }
            else if (Fix64.Abs(hitPoint.y - maxY) < epsilon)
            {
                hitNormal = FixVector2.Up; // 上侧面
            }
            else
            {
                // 默认法线（指向射线起点）
                hitNormal = (rayOrigin - hitPoint);
                hitNormal.Normalize();
            }
            
            return true;
        }
    }
    
    /// <summary>
    /// 射线检测结果
    /// </summary>
    public struct RaycastHit2D
    {
        /// <summary>
        /// 被击中的Entity
        /// </summary>
        public Entity entity;
        
        /// <summary>
        /// 击中点（世界坐标）
        /// </summary>
        public FixVector2 point;
        
        /// <summary>
        /// 击中点的法线
        /// </summary>
        public FixVector2 normal;
        
        /// <summary>
        /// 从射线起点到击中点的距离
        /// </summary>
        public Fix64 distance;
    }
}