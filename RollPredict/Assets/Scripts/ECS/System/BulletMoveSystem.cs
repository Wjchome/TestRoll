using System.Collections.Generic;
using Frame.FixMath;
using Proto;

namespace Frame.ECS
{
    public class BulletMoveSystem:ISystem
    {
        public void Execute(World world, List<FrameData> inputs)
        {
            var bulletsToUpdate = new List<Entity>();
            
            // 收集所有子弹Entity
            foreach (var entity in world.GetEntitiesWithComponent<BulletComponent>())
            {
                bulletsToUpdate.Add(entity);
            }

            // 更新每个子弹
            foreach (var bulletEntity in bulletsToUpdate)
            {
                if (!world.TryGetComponent<BulletComponent>(bulletEntity, out var bulletComponent))
                    continue;

                // 更新子弹位置
                FixVector2 newPosition = bulletComponent.position + bulletComponent.velocity;

                // 检查子弹是否超出边界（简单边界检查，可以扩展）
                // 这里假设世界边界是 -10 到 10
                if (newPosition.x < (Fix64)(-10) || newPosition.x > (Fix64)10 ||
                    newPosition.y < (Fix64)(-10) || newPosition.y > (Fix64)10)
                {
                    // 子弹超出边界，销毁
                    world.DestroyEntity(bulletEntity);
                    continue;
                }

                // 更新BulletComponent
                var updatedComponent = new BulletComponent(
                    newPosition,
                    bulletComponent.velocity,
                    bulletComponent.ownerEntityId
                );
                world.AddComponent(bulletEntity, updatedComponent);
            }
        }
   
    }
}