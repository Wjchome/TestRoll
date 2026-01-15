using System.Collections.Generic;
using System.Linq;
using Frame.FixMath;
using Proto;

namespace Frame.ECS
{
    /// <summary>
    /// 僵尸AI系统：处理僵尸的寻路和移动
    /// </summary>
    public class ZombieAISystem : ISystem
    {
        // 寻路冷却时间（帧数）
        private const int PATHFINDING_COOLDOWN_FRAMES = 10;
        private const int ATTACK_COOLDOWN_FRAMES = 2;

        public void Execute(World world, List<FrameData> inputs)
        {
            var map = new GridMapComponent();

            foreach (var (mapEntity, _map) in world.GetEntitiesWithComponents<GridMapComponent>())
            {
                map = _map;
            }

            // 获取所有玩家位置（用于寻找最近玩家）
            var playerPositions = new List<FixVector2>();
            foreach (var (playerEntity, playerTransform, _) in world
                         .GetEntitiesWithComponents<Transform2DComponent, PlayerComponent>())
            {
                playerPositions.Add(playerTransform.position);
            }

            // 获取所有僵尸
            foreach (var (entity, transform, ai, velocityComponent) in
                     world.GetEntitiesWithComponents<Transform2DComponent, ZombieAIComponent, VelocityComponent>())
            {
                // 检查僵直状态（僵直状态下无法移动和攻击）
                if (world.TryGetComponent<StiffComponent>(entity, out var stiff))
                {
                    if (stiff.IsStiff)
                    {
                        continue; // 跳过AI逻辑
                    }
                }

                var updatedAI = ai;
                var newVelocity = velocityComponent;

                if (playerPositions.Count == 0)
                {
                    continue;
                }

                // 排序所有玩家位置（按距离从近到远）
                var sortedPlayers = new List<(FixVector2 position, Fix64 distance)>();
                foreach (var playerPos in playerPositions)
                {
                    FixVector2 diff = playerPos - transform.position;
                    Fix64 distance = Fix64.Sqrt(diff.x * diff.x + diff.y * diff.y);
                    sortedPlayers.Add((playerPos, distance));
                }
                // 按距离排序（最近的在前面）
                sortedPlayers.Sort((a, b) => a.distance.CompareTo(b.distance));

                switch (ai.state)
                {
                    case ZombieState.Chase:

                        // 攻击范围检测（每两帧检测一次）
                        if (updatedAI.attackDetectionCooldown > 0)
                        {
                            updatedAI.attackDetectionCooldown--;
                        }
                        else
                        {
                            // 重置冷却（每两帧检测一次）
                            updatedAI.attackDetectionCooldown = ATTACK_COOLDOWN_FRAMES;

                            // 使用QuadTree查询攻击范围内的玩家
                            var physicsSystem = ECSStateMachine.GetSystem<PhysicsSystem>();

                            var playersInRange = physicsSystem.QueryCircleRegion(
                                world,
                                transform.position,
                                updatedAI.attackCheckRange,
                                (int)PhysicsLayer.Player // 只查询玩家层
                            );

                            if (playersInRange.Count > 0)
                            {
                                // 找到最近的玩家，进入攻击状态
                                FixVector2 nearestPlayerTransform = FixVector2.Zero;
                                Fix64 minDist = Fix64.MaxValue;

                                foreach (var playerEntity in playersInRange)
                                {
                                    if (world.TryGetComponent<Transform2DComponent>(playerEntity,
                                            out var playerTransform))
                                    {
                                        FixVector2 diff = playerTransform.position - transform.position;
                                        Fix64 dist = Fix64.Sqrt(diff.x * diff.x + diff.y * diff.y);
                                        if (dist < minDist)
                                        {
                                            minDist = dist;
                                            nearestPlayerTransform = playerTransform.position;
                                        }
                                    }
                                }


                                // 计算攻击方向
                                FixVector2 toPlayer = nearestPlayerTransform - transform.position;
                                if (toPlayer.SqrMagnitude() > Fix64.Zero)
                                {
                                    toPlayer.Normalize();

                                    // 进入攻击前摇状态
                                    updatedAI.state = ZombieState.AttackWindup;
                                    updatedAI.attackDirection = toPlayer;
                                    updatedAI.attackWindupTimer = updatedAI.attackWindupFrames;

                                    // 停止移动
                                    newVelocity.velocity = FixVector2.Zero;
                                    world.AddComponent(entity, newVelocity);
                                    world.AddComponent(entity, updatedAI);
                                    continue;
                                }
                            }
                        }


                        // 正常寻路和移动逻辑
                        if (ai.pathfindingCooldown > 0)
                        {
                            updatedAI.pathfindingCooldown--;
                            world.AddComponent(entity, updatedAI);
                        }
                        else
                        {
                            // 按顺序尝试寻路：最近、第二近、第三近...
                            List<FixVector2> foundPath = null;
                            FixVector2 targetPos = FixVector2.Zero;
                            
                            foreach (var (playerPos, _) in sortedPlayers)
                            {
                                var path = DeterministicPathfinding.FindPath(map, transform.position, playerPos);
                                if (path != null && path.Count > 0)
                                {
                                    // 找到可通行路径，使用这个路径
                                    foundPath = path;
                                    targetPos = playerPos;
                                    break;
                                }
                            }
                            
                            // 如果找到路径，使用路径
                            if (foundPath != null)
                            {
                                updatedAI.targetPosition = targetPos;
                                updatedAI.currentPath = foundPath;
                                updatedAI.currentPathIndex = 0;
                                updatedAI.pathfindingCooldown = PATHFINDING_COOLDOWN_FRAMES;
                            }
                            else
                            {
                                // 所有玩家都找不到可通行路径，使用直线移动最近的玩家
                                updatedAI.targetPosition = sortedPlayers[0].position;
                                updatedAI.currentPath = null;
                                updatedAI.currentPathIndex = 0;
                                updatedAI.pathfindingCooldown = PATHFINDING_COOLDOWN_FRAMES;
                            }
                            
                            world.AddComponent(entity, updatedAI);
                        }

                        // 移动逻辑
                        if (updatedAI.currentPath != null && updatedAI.currentPath.Count > 0 &&
                            updatedAI.currentPathIndex < updatedAI.currentPath.Count)
                        {
                            // 按路径移动
                            FixVector2 targetPoint = updatedAI.currentPath[updatedAI.currentPathIndex];
                            FixVector2 direction = targetPoint - transform.position;
                            Fix64 distance = Fix64.Sqrt(direction.x * direction.x + direction.y * direction.y);

                            // 如果到达当前路径点，移动到下一个点
                            if (distance < map.cellSize / Fix64.Two)
                            {
                                updatedAI.currentPathIndex++;
                                world.AddComponent(entity, updatedAI);
                            }
                            else
                            {
                                // 移动到目标点
                                direction.Normalize();
                                AddForceHelper.ApplyForce(world, entity, direction * updatedAI.moveSpeed);
                            }
                        }
                        else
                        {
                            // 没有路径或路径已走完，直线移动最近的玩家
                            FixVector2 direction = sortedPlayers[0].position - transform.position;
                            Fix64 dirMagnitude = Fix64.Sqrt(direction.x * direction.x + direction.y * direction.y);
                            
                            // 避免除零
                            if (dirMagnitude > Fix64.Zero)
                            {
                                direction = direction / dirMagnitude; // 归一化
                                AddForceHelper.ApplyForce(world, entity, direction * updatedAI.moveSpeed);
                            }
                        }

                        break;

                    case ZombieState.AttackWindup:
                        // 前摇计时
                        updatedAI.attackWindupTimer--;
                        if (updatedAI.attackWindupTimer <= 0)
                        {
                            // 进入攻击判定帧
                            updatedAI.state = ZombieState.Attack;
                        }


                        world.AddComponent(entity, updatedAI);
                        break;

                    case ZombieState.Attack:
                        // 伤害判定帧：检测前方扇形区域内的玩家
                        CheckAttackDamage(world, transform.position, updatedAI);

                        // 立即进入后摇状态
                        updatedAI.state = ZombieState.AttackCooldown;
                        updatedAI.attackCooldownTimer = updatedAI.attackCooldownFrames;

                        world.AddComponent(entity, updatedAI);
                        break;

                    case ZombieState.AttackCooldown:
                        // 后摇计时
                        updatedAI.attackCooldownTimer--;
                        if (updatedAI.attackCooldownTimer <= 0)
                        {
                            // 回到追逐状态
                            updatedAI.state = ZombieState.Chase;
                        }


                        world.AddComponent(entity, updatedAI);
                        break;
                }
            }
        }

        /// <summary>
        /// 检查攻击伤害：使用旋转矩形查询检测前方区域内的玩家
        /// </summary>
        private void CheckAttackDamage(World world, FixVector2 zombiePosition, ZombieAIComponent ai)
        {
            // 1. 将攻击方向转换为旋转角度（弧度）
            // Atan2(y, x) 返回从x轴正方向到向量的角度
            Fix64 rotation = Fix64.Atan2(ai.attackDirection.y, ai.attackDirection.x);

            // 2. 计算矩形尺寸
            // 长度 = 攻击距离
            Fix64 rectLength = ai.attackDamageLength;


            FixVector2 rectSize = new FixVector2(rectLength, ai.attackDamageWidth);

            // 3. 计算矩形中心位置（在僵尸前方，距离为长度的一半）
            FixVector2 rectCenter = zombiePosition + ai.attackDirection * (rectLength / Fix64.Two);

            // 4. 使用PhysicsSystem查询旋转矩形区域内的玩家
            var physicsSystem = ECSStateMachine.GetSystem<PhysicsSystem>();
            if (physicsSystem != null)
            {
                var playersInRect = physicsSystem.QueryRotatedRectRegion(
                    world,
                    rectCenter,
                    rectSize,
                    rotation,
                    (int)PhysicsLayer.Player // 只查询玩家层
                );

                // 5. 对查询结果中的玩家造成伤害
                foreach (var playerEntity in playersInRect)
                {
                    HPDamageHelper.ApplyDamage(world, playerEntity, ai.attackDamage);

                    AddForceHelper.ApplyForce(world, playerEntity, ai.attackDirection / (Fix64)3);
                }
            }
        }
    }
}