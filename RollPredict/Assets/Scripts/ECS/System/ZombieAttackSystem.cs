// using System.Collections.Generic;
// using Frame.FixMath;
// using Proto;
//
// namespace Frame.ECS
// {
//     /// <summary>
//     /// 僵尸攻击系统：处理僵尸攻击的完整流程
//     /// 
//     /// 攻击流程：
//     /// 1. Chase状态：检测玩家是否进入攻击范围
//     /// 2. AttackWindup状态：前摇计时，停止移动
//     /// 3. Attack状态：伤害判定帧，检测前方区域玩家
//     /// 4. AttackCooldown状态：后摇计时，停止移动
//     /// 5. 回到Chase状态：继续追逐
//     /// </summary>
//     public class ZombieAttackSystem : ISystem
//     {
//         public void Execute(World world, List<FrameData> inputs)
//         {
//             foreach (var (zombieEntity, transform, ai, attack) in
//                 world.GetEntitiesWithComponents<Transform2DComponent, ZombieAIComponent, ZombieAttackComponent>())
//             {
//                 var updatedAI = ai;
//
//                 switch (ai.state)
//                 {
//                     case ZombieState.Chase:
//                         // 检查是否进入攻击范围
//                         var nearestPlayer = FindNearestPlayerInRange(world, transform.position, attack.attackRange);
//                         if (nearestPlayer.HasValue)
//                         {
//                             // 计算攻击方向（从僵尸指向玩家）
//                             FixVector2 toPlayer = nearestPlayer.Value.position - transform.position;
//                             Fix64 distance = Fix64.Sqrt(toPlayer.x * toPlayer.x + toPlayer.y * toPlayer.y);
//                             
//                             if (distance > Fix64.Zero)
//                             {
//                                 toPlayer.Normalize();
//                                 
//                                 // 进入攻击前摇状态
//                                 updatedAI.state = ZombieState.AttackWindup;
//                                 updatedAI.attackDirection = toPlayer;
//                                 updatedAI.targetPlayerEntityId = nearestPlayer.Value.entity.Id;
//                                 updatedAI.attackWindupTimer = attack.windupFrames;
//                             }
//                         }
//                         break;
//
//                     case ZombieState.AttackWindup:
//                         // 前摇计时
//                         updatedAI.attackWindupTimer--;
//                         if (updatedAI.attackWindupTimer <= 0)
//                         {
//                             // 进入攻击判定帧
//                             updatedAI.state = ZombieState.Attack;
//                         }
//                         break;
//
//                     case ZombieState.Attack:
//                         // 伤害判定帧：检测前方区域内的玩家
//                         CheckAttackDamage(world, zombieEntity, transform.position, updatedAI, attack);
//                         
//                         // 立即进入后摇状态
//                         updatedAI.state = ZombieState.AttackCooldown;
//                         updatedAI.attackCooldownTimer = attack.cooldownFrames;
//                         break;
//
//                     case ZombieState.AttackCooldown:
//                         // 后摇计时
//                         updatedAI.attackCooldownTimer--;
//                         if (updatedAI.attackCooldownTimer <= 0)
//                         {
//                             // 回到追逐状态
//                             updatedAI.state = ZombieState.Chase;
//                             updatedAI.targetPlayerEntityId = -1; // 清除目标锁定
//                         }
//                         break;
//                 }
//
//                 world.AddComponent(zombieEntity, updatedAI);
//             }
//         }
//
//         /// <summary>
//         /// 查找攻击范围内的最近玩家
//         /// </summary>
//         private (Entity entity, Transform2DComponent position)? FindNearestPlayerInRange(
//             World world, FixVector2 zombiePosition, Fix64 attackRange)
//         {
//             Entity? nearestEntity = null;
//             Transform2DComponent? nearestTransform = null;
//             Fix64 minDistance = attackRange;
//
//             foreach (var (playerEntity, playerTransform, _) in
//                 world.GetEntitiesWithComponents<Transform2DComponent, PlayerComponent>())
//             {
//                 FixVector2 diff = playerTransform.position - zombiePosition;
//                 Fix64 distance = Fix64.Sqrt(diff.x * diff.x + diff.y * diff.y);
//
//                 if (distance <= attackRange && distance < minDistance)
//                 {
//                     minDistance = distance;
//                     nearestEntity = playerEntity;
//                     nearestTransform = playerTransform;
//                 }
//             }
//
//             if (nearestEntity.HasValue && nearestTransform.HasValue)
//             {
//                 return (nearestEntity.Value, nearestTransform.Value);
//             }
//
//             return null;
//         }
//
//         /// <summary>
//         /// 检查攻击伤害：检测前方扇形区域内的玩家
//         /// </summary>
//         private void CheckAttackDamage(World world, Entity zombieEntity,
//             FixVector2 zombiePosition, ZombieAIComponent ai, ZombieAttackComponent attack)
//         {
//             FixVector2 attackDir = ai.attackDirection;
//             Fix64 halfAngle = attack.attackDamageAngle / Fix64.Two;
//
//             foreach (var (playerEntity, playerTransform, playerComponent) in
//                 world.GetEntitiesWithComponents<Transform2DComponent, PlayerComponent>())
//             {
//                 FixVector2 toPlayer = playerTransform.position - zombiePosition;
//                 Fix64 distance = Fix64.Sqrt(toPlayer.x * toPlayer.x + toPlayer.y * toPlayer.y);
//
//                 // 检查距离
//                 if (distance > attack.attackDamageRange)
//                     continue;
//
//                 // 检查角度（扇形判定）
//                 if (distance > Fix64.Zero)
//                 {
//                     toPlayer.Normalize();
//                     Fix64 dot = FixVector2.Dot(attackDir, toPlayer);
//                     
//                     // 使用点积计算角度（避免使用Acos，提高性能）
//                     // cos(angle) = dot，如果 angle <= halfAngle，则 cos(angle) >= cos(halfAngle)
//                     Fix64 cosHalfAngle = Fix64.Cos(halfAngle);
//                     
//                     if (dot >= cosHalfAngle)
//                     {
//                         // 玩家在攻击范围内，造成伤害
//                         ApplyDamage(world, playerEntity, attack.damage);
//                     }
//                 }
//             }
//         }
//
//         /// <summary>
//         /// 对玩家造成伤害
//         /// </summary>
//         private void ApplyDamage(World world, Entity playerEntity, int damage)
//         {
//             if (world.TryGetComponent<PlayerComponent>(playerEntity, out var player))
//             {
//                 var updatedPlayer = player;
//                 updatedPlayer.HP = System.Math.Max(0, updatedPlayer.HP - damage);
//                 world.AddComponent(playerEntity, updatedPlayer);
//
//                 // 如果血量 <= 0，可以在这里触发死亡事件（后续实现）
//                 // if (updatedPlayer.HP <= 0)
//                 // {
//                 //     HandlePlayerDeath(world, playerEntity);
//                 // }
//             }
//         }
//     }
// }
//
