using System.Collections.Generic;
using Frame.FixMath;
using Proto;
using UnityEngine;

namespace Frame.ECS
{
    public class PlayerShootSystem:ISystem
    {
        
        public static Fix64 BulletSpeed = (Fix64)0.2f;


        public void Execute(World world, List<FrameData> inputs)
        {
            foreach (var frameData in inputs)
            {
                // 检查是否发射子弹
                if (!frameData.IsFire)
                    continue;

                var playerId = frameData.PlayerId;

                // 查找玩家的Entity
                Entity? playerEntity = FindPlayerEntity(world, playerId);
                if (!playerEntity.HasValue)
                    continue;
                // 获取PlayerComponent并检查冷却
                if (!world.TryGetComponent<PlayerComponent>(playerEntity.Value, out var playerComponent))
                    continue;
                
                // 检查是否在发射子弹模式
                if (playerComponent.currentIndex != 1)
                {
                    continue; // 不是发射子弹模式
                }
                
                // 检查子弹冷却
                if (playerComponent.bulletCooldownTimer > Fix64.Zero)
                {
                    continue; // 冷却中，不允许发射
                }

                // 获取Transform组件
                if (!world.TryGetComponent<Transform2DComponent>(playerEntity.Value, out var playerTransform2DComponent))
                    continue;

                // 计算子弹方向（从玩家位置指向目标位置）
                FixVector2 bulletPosition = playerTransform2DComponent.position;
                FixVector2 targetPosition =
                    new FixVector2(Fix64.FromRaw(frameData.FireX), Fix64.FromRaw(frameData.FireY));

                // 计算方向向量并归一化
                FixVector2 direction = targetPosition - bulletPosition;
                Fix64 distance = Fix64.Sqrt(direction.x * direction.x + direction.y * direction.y);

                if (distance > Fix64.Zero)
                {
                    direction.Normalize();
                    FixVector2 bulletVelocity = direction * BulletSpeed;


                    Entity bulletEntity = world.CreateEntity();
                    var bulletComponent = new BulletComponent( playerEntity.Value.Id );
                    var transform2DComponent = new Transform2DComponent(playerTransform2DComponent.position);
                    var physicsBodyComponent = new PhysicsBodyComponent(Fix64.One, false, false, true, Fix64.Zero
                        , Fix64.Zero, Fix64.Zero,(int)PhysicsLayer.Bullet);
                    var collisionShapeComponent = CollisionShapeComponent.CreateCircle((Fix64)0.25);
                    var velocityComponent = new VelocityComponent(bulletVelocity);
                    
                    world.AddComponent(bulletEntity, bulletComponent);
                    world.AddComponent(bulletEntity, transform2DComponent);
                    world.AddComponent(bulletEntity, physicsBodyComponent);
                    world.AddComponent(bulletEntity, collisionShapeComponent);
                    world.AddComponent(bulletEntity, velocityComponent);
                    
                    // 应用子弹冷却
                    var updatedPlayer = playerComponent;
                    updatedPlayer.bulletCooldownTimer = PlayerComponent.BulletCooldownDuration;
                    world.AddComponent(playerEntity.Value, updatedPlayer);
                }
            }
        }

        private static Entity? FindPlayerEntity(World world, int playerId)
            {
                foreach (var entity in world.GetEntitiesWithComponent<PlayerComponent>())
                {
                    if (world.TryGetComponent<PlayerComponent>(entity, out var playerComponent))
                    {
                        if (playerComponent.playerId == playerId)
                        {
                            return entity;
                        }
                    }
                }
                return null;
            }
    }
}