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
                        HPDamageHelper.ApplyDamage(world, playerEntity, bulletComponent.damage);
                        
                        // 添加击退效果（可选）
                        AddForceHelper.ApplyForce(world,playerEntity,velocityComponent.velocity);
                        
                        removedEntities.Add(entity);
                    }
                    else if (world.TryGetComponent<ZombieAIComponent>(new Entity(entityId), out var zombieAIComponent))
                    {
                        Entity zombieEntity = new Entity(entityId);
                        HPDamageHelper.ApplyDamage(world, zombieEntity, bulletComponent.damage);

                        AddForceHelper. ApplyForce(world,zombieEntity,velocityComponent.velocity);
                        
                        // 碰到敌人，销毁
                        removedEntities.Add(entity);
                    }
                    else if (world.TryGetComponent<WallComponent>(new Entity(entityId), out var wallComponent))
                    {
                        Entity wallEntity = new Entity(entityId);
                        
                        // 子弹击中墙，造成伤害（DeathSystem会处理死亡逻辑）
                        HPDamageHelper.ApplyDamage(world, wallEntity, bulletComponent.damage);
                        
                        // 子弹击中墙后销毁
                        removedEntities.Add(entity);
                    }
                    else if (world.TryGetComponent<BarrelComponent>(new Entity(entityId), out var barrelComponent))
                    {
                        Entity barrelEntity = new Entity(entityId);
                        
                        HPDamageHelper.ApplyDamage(world, barrelEntity, bulletComponent.damage);
                        
                        // 子弹击中油桶后销毁
                        removedEntities.Add(entity);
                    }
                }
            }

            // 销毁子弹等实体
            foreach (var entity in removedEntities)
            {
                world.DestroyEntity(entity);
            }
        }
    }
}