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
                var updatedAI = ai;
                var newVelocity = velocityComponent;

                // 寻找最近的玩家
                FixVector2 nearestPlayerPos = FixVector2.Zero;
                Fix64 minDistance = Fix64.MaxValue;

                foreach (var playerPos in playerPositions)
                {
                    FixVector2 diff = playerPos - transform.position;
                    Fix64 distance = Fix64.Sqrt(diff.x * diff.x + diff.y * diff.y);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestPlayerPos = playerPos;
                    }
                }
                if (playerPositions.Count == 0)
                {
                    continue;
                }
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
                            updatedAI.attackDetectionCooldown =  ATTACK_COOLDOWN_FRAMES;

                            // 使用QuadTree查询攻击范围内的玩家
                            var physicsSystem = ECSStateMachine.GetSystem<PhysicsSystem>();
                            if (physicsSystem != null)
                            {
                                var playersInRange = physicsSystem.QueryCircleRegion(
                                    world,
                                    transform.position,
                                    updatedAI.attackRange,
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
                        }

                        // 正常寻路和移动逻辑
                        if (ai.pathfindingCooldown > 0)
                        {
                            updatedAI.pathfindingCooldown--;
                            world.AddComponent(entity, updatedAI);
                        }
                        else
                        {
                            

                            updatedAI.targetPosition = nearestPlayerPos;
                            updatedAI.currentPath = null;
                            updatedAI.currentPathIndex = 0;
                            var path = DeterministicPathfinding.FindPath(map, transform.position,
                                updatedAI.targetPosition);
                            if (path != null && path.Count > 0)
                            {
                                updatedAI.pathfindingCooldown = PATHFINDING_COOLDOWN_FRAMES;
                            }

                            updatedAI.currentPath = path;
                            updatedAI.currentPathIndex = 0;

                            world.AddComponent(entity, updatedAI);
                        }

                        if (updatedAI.currentPath != null && updatedAI.currentPath.Count > 0 &&
                            updatedAI.currentPathIndex < updatedAI.currentPath.Count)
                        {
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
                                newVelocity.velocity += direction * updatedAI.moveSpeed;
                                world.AddComponent(entity, newVelocity);
                            }
                        }
                        else
                        {
                            FixVector2 direction = nearestPlayerPos - transform.position;
                            direction.Normalize();
                            newVelocity.velocity += direction * updatedAI.moveSpeed;
                            world.AddComponent(entity, newVelocity);
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
        /// 检查攻击伤害：检测前方扇形区域内的玩家
        /// </summary>
        private void CheckAttackDamage(World world, FixVector2 zombiePosition, ZombieAIComponent ai)
        {
            FixVector2 attackDir = ai.attackDirection;
            Fix64 halfAngle = ai.attackDamageAngle / Fix64.Two;

            foreach (var (playerEntity, playerTransform, playerComponent) in
                     world.GetEntitiesWithComponents<Transform2DComponent, PlayerComponent>())
            {
                FixVector2 toPlayer = playerTransform.position - zombiePosition;
                Fix64 distance = Fix64.Sqrt(toPlayer.x * toPlayer.x + toPlayer.y * toPlayer.y);

                // 检查距离
                if (distance > ai.attackDamageRange)
                    continue;

                // 检查角度（扇形判定）
                if (distance > Fix64.Zero)
                {
                    toPlayer.Normalize();
                    Fix64 dot = FixVector2.Dot(attackDir, toPlayer);

                    // 使用点积计算角度（避免使用Acos，提高性能）
                    // cos(angle) = dot，如果 angle <= halfAngle，则 cos(angle) >= cos(halfAngle)
                    Fix64 cosHalfAngle = Fix64.Cos(halfAngle);

                    if (dot >= cosHalfAngle)
                    {
                        // 玩家在攻击范围内，造成伤害
                        ApplyDamage(world, playerEntity, ai.attackDamage);
                    }
                }
            }
        }

        /// <summary>
        /// 对玩家造成伤害
        /// </summary>
        private void ApplyDamage(World world, Entity playerEntity, int damage)
        {
            if (world.TryGetComponent<PlayerComponent>(playerEntity, out var player))
            {
                var updatedPlayer = player;
                updatedPlayer.HP = System.Math.Max(0, updatedPlayer.HP - damage);
                world.AddComponent(playerEntity, updatedPlayer);

                // 如果血量 <= 0，可以在这里触发死亡事件（后续实现）
                // if (updatedPlayer.HP <= 0)
                // {
                //     HandlePlayerDeath(world, playerEntity);
                // }
            }
        }
    }
}