using System.Collections.Generic;
using Frame.FixMath;
using Proto;
using UnityEngine;

namespace Frame.ECS
{
    public class BulletCheckSystem : ISystem
    {
        public void Execute(World world, List<FrameData> inputs)
        {
            List<Entity> removedEntities = new List<Entity>();
            // 收集所有子弹Entity
            foreach (var (entity, bulletComponent, velocityComponent, transform2DComponent, collisionComponent) in world
                         .GetEntitiesWithComponents<
                             BulletComponent, VelocityComponent, Transform2DComponent, CollisionComponent>())
            {
                if (transform2DComponent.position.x < (Fix64)(-10) || transform2DComponent.position.x > (Fix64)10 ||
                    transform2DComponent.position.y < (Fix64)(-10) || transform2DComponent.position.y > (Fix64)10)
                {
                    // 子弹超出边界，销毁
                    removedEntities.Add(entity);
                }

                foreach (int entityId in collisionComponent.GetAllCollisions())
                {
                    if (bulletComponent.ownerEntityId == entityId)
                    {
                        Debug.Log("发射者");
                    }
                    else if (world.TryGetComponent<PlayerComponent>(new Entity(entityId), out var playerComponent))
                    {
                        // 子弹击中玩家，造成伤害
                        Entity playerEntity = new Entity(entityId);
                        PlayerDamageHelper.ApplyDamage(world, playerEntity, bulletComponent.damage);
                        
                       
                        // 添加击退效果（可选）
                        AddForceHelper. ApplyForce(world,playerEntity,velocityComponent.velocity);
                        // if (world.TryGetComponent<VelocityComponent>(playerEntity, out var playerVelocityComponent))
                        // {
                        //     // 计算击退方向（从子弹位置指向玩家位置）
                        //     if (world.TryGetComponent<Transform2DComponent>(playerEntity, out var playerTransform))
                        //     {
                        //         FixVector2 knockbackDir = playerTransform.position - transform2DComponent.position;
                        //         if (knockbackDir.SqrMagnitude() > Fix64.Zero)
                        //         {
                        //             knockbackDir.Normalize();
                        //             // 击退力度（可以根据需要调整）
                        //             Fix64 knockbackForce = (Fix64)0.1f;
                        //             playerVelocityComponent.velocity += knockbackDir * knockbackForce;
                        //             world.AddComponent(playerEntity, playerVelocityComponent);
                        //         }
                        //     }
                        // }

                        // 子弹击中玩家后销毁
                        removedEntities.Add(entity);
                    }
                    else if (world.TryGetComponent<ZombieAIComponent>(new Entity(entityId), out var zombieAIComponent))
                    {
                        Entity zombieEntity = new Entity(entityId);
                        
                        AddForceHelper. ApplyForce(world,zombieEntity,velocityComponent.velocity);
                        
                        // if (world.TryGetComponent<VelocityComponent>(new Entity(entityId),
                        //         out var zombieVelocityComponent))
                        // {
                        //     zombieVelocityComponent.velocity += velocityComponent.velocity;
                        //     world.AddComponent(new Entity(entityId), zombieVelocityComponent);
                        // }

                        // 碰到敌人，销毁
                        removedEntities.Add(entity);
                    }
                    else if (world.TryGetComponent<WallComponent>(new Entity(entityId), out var wallComponent))
                    {
                       

                        // 碰到敌人，销毁
                        removedEntities.Add(entity);
                    }
                }
            }

            foreach (var entity in removedEntities)
            {
                world.DestroyEntity(entity);
            }
        }
    }
}