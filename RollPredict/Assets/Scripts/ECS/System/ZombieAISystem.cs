using System.Collections.Generic;
using System.Linq;
using Frame.FixMath;
using Proto;
using Unity.Collections;
using UnityEngine;

namespace Frame.ECS
{
    /// <summary>
    /// 僵尸AI系统：处理僵尸的寻路和移动
    /// 使用流场（Flow Field）算法优化性能
    /// 
    /// 注意：System不保存任何状态，所有状态都存储在Component中
    /// 这样确保预测回滚系统可以正确恢复状态
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

            // 获取所有玩家位置（用于寻找最近玩家和计算流场）
            var playerPositions = new List<FixVector2>();
            foreach (var (playerEntity, playerTransform, _) in world
                         .GetEntitiesWithComponents<Transform2DComponent, PlayerComponent>())
            {
                playerPositions.Add(playerTransform.position);
            }

            if (playerPositions.Count == 0)
            {
                return; // 没有玩家，直接返回
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


                        // 使用流场寻路（优化性能）
                        // 流场已经在循环外计算完成，所有僵尸共享同一个流场
                        FixVector2 nearestPlayerPos = sortedPlayers[0].position;

                        foreach (var (_, flowFieldComponent) in
                                 world.GetEntitiesWithComponents<FlowFieldComponent>())
                        {
                            if (flowFieldComponent.gradientField != null)
                            {
                                // 查询流场方向
                                FixVector2 flowDirection = FlowFieldPathfinding.GetDirection(
                                    flowFieldComponent.gradientField,
                                    map,
                                    transform.position
                                );

                                // 检查流场方向是否有效
                                if (flowDirection.SqrMagnitude() > Fix64.Zero)
                                {
                                    // 使用流场方向移动
                                    updatedAI.targetPosition = nearestPlayerPos;
                                    AddForceHelper.ApplyForce(world, entity, flowDirection * updatedAI.moveSpeed);
                                }
                                else
                                {
                                    // 流场方向为零（可能是目标点或不可达），使用直线移动（fallback）
                                    FixVector2 direction = nearestPlayerPos - transform.position;
                                    Fix64 dirMagnitude =
                                        Fix64.Sqrt(direction.x * direction.x + direction.y * direction.y);

                                    if (dirMagnitude > Fix64.Zero)
                                    {
                                        direction = direction / dirMagnitude;
                                        AddForceHelper.ApplyForce(world, entity, direction * updatedAI.moveSpeed);
                                    }
                                }
                            }
                            else
                            {
                                Debug.LogError("qw");
                                // 流场未计算（冷却时间未到或计算失败），使用直线移动（fallback）
                                FixVector2 direction = nearestPlayerPos - transform.position;
                                Fix64 dirMagnitude =
                                    Fix64.Sqrt(direction.x * direction.x + direction.y * direction.y);

                                if (dirMagnitude > Fix64.Zero)
                                {
                                    direction = direction / dirMagnitude;
                                    AddForceHelper.ApplyForce(world, entity, direction * updatedAI.moveSpeed);
                                }
                            }
                        }


                        world.AddComponent(entity, updatedAI);

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