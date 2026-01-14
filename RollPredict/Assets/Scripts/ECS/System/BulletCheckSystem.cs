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
                        if (world.TryGetComponent<VelocityComponent>(new Entity(entityId),
                                out var playerVelocityComponent))
                        {
                            playerVelocityComponent.velocity += velocityComponent.velocity;
                            world.AddComponent(new Entity(entityId), playerVelocityComponent);
                        }

                        // 碰到敌人，销毁
                        removedEntities.Add(entity);
                    }
                    else if (world.TryGetComponent<ZombieAIComponent>(new Entity(entityId), out var zombieAIComponent))
                    {
                        if (world.TryGetComponent<VelocityComponent>(new Entity(entityId),
                                out var zombieVelocityComponent))
                        {
                            zombieVelocityComponent.velocity += velocityComponent.velocity;
                            world.AddComponent(new Entity(entityId), zombieVelocityComponent);
                        }

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