using System.Collections.Generic;
using Frame.FixMath;
using Proto;
using UnityEngine;

namespace Frame.ECS
{
    public class BulletCheckSystem:ISystem
    {
        public void Execute(World world, List<FrameData> inputs)
        {
            
            // 收集所有子弹Entity
            foreach (var entity in world.CreateQuery().WithAll<BulletComponent>().GetEntities())
            {
                if(world.TryGetComponent<BulletComponent>(entity, out var bulletComponent))
                {
                }
                if(world.TryGetComponent<VelocityComponent>(entity, out var velocityComponent))
                {
                }
                if (world.TryGetComponent<Transform2DComponent>(entity, out var transform2DComponent))
                {
                    if (transform2DComponent.position.x < (Fix64)(-10) || transform2DComponent.position.x > (Fix64)10 ||
                        transform2DComponent.position.y < (Fix64)(-10) || transform2DComponent.position.y > (Fix64)10)
                    {
                        // 子弹超出边界，销毁
                        world.DestroyEntity(entity);
                    }
                }

                if (world.TryGetComponent<CollisionComponent>(entity, out var collisionComponent))
                {
                    foreach (int entityId in collisionComponent.GetAllCollisions())
                    {
                        if (bulletComponent.ownerEntityId == entityId)
                        {
                            Debug.Log("发射者");
                        }
                        else if(world.TryGetComponent<PlayerComponent>(new Entity( entityId), out var playerComponent))
                        {
                            
                            if(world.TryGetComponent<VelocityComponent>(new Entity( entityId), out var playerVelocityComponent))
                            {
                                playerVelocityComponent.velocity+=velocityComponent.velocity;
                                world.AddComponent(new Entity( entityId),playerVelocityComponent);
                            }
                            
                            // 碰到敌人，销毁
                            world.DestroyEntity(entity);
                        }
                    }

                    
                }
               
            }

   
            
        }
   
    }
}