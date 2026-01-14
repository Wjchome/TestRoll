using System.Collections.Generic;
using System.Linq;
using Frame.FixMath;
using Frame.Physics2D;
using Proto;

namespace Frame.ECS
{
    /// <summary>
    /// 物理系统：处理物理模拟和碰撞检测
    /// </summary>
    public class PhysicsSystem : ISystem
    {
        // 物理世界配置
        public FixVector2 gravity = FixVector2.Down;
        public int iterations = 4;  // 碰撞分离迭代次数
        public int subSteps = 2;    // 子步迭代次数

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
                UpdateSingleStep(world,  subStepDeltaTime);
            }
        }

        private void UpdateSingleStep(World world,  Fix64 deltaTime)
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
            foreach (var (entity,body) in world.GetEntitiesWithComponents<PhysicsBodyComponent>())
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
        private void UpdatePositions(World world,  Fix64 deltaTime)
        {
            foreach (var (entity,body,transform,velocity) in world.GetEntitiesWithComponents<PhysicsBodyComponent,Transform2DComponent,VelocityComponent>())
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
                    Fix64 dampingFactor = Fix64.One - Fix64.Clamp(body.linearDamping * deltaTime, Fix64.Zero, Fix64.One);
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
            FixRect quadTreeBound = new FixRect(Fix64.Zero,Fix64.Zero, Fix64.Zero, Fix64.Zero);
            foreach (var (entity,transform,shape) in world.GetEntitiesWithComponents<Transform2DComponent,CollisionShapeComponent>())
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
            

            foreach (var (entityA,bodyA,transformA,shapeA) in world.GetEntitiesWithComponents<PhysicsBodyComponent,Transform2DComponent,CollisionShapeComponent>())
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
                    var pair = (entityA.Id < entityB.Id) ? (entityA, entityB) : ( entityB, entityA);
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
            foreach (var (entity,_) in world.GetEntitiesWithComponents<PhysicsBodyComponent>())
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


    }
}


