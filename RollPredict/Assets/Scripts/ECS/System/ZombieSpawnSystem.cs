using System.Collections.Generic;
using System.Linq;
using Frame.FixMath;
using Proto;

namespace Frame.ECS
{
    /// <summary>
    /// 僵尸生成系统：在游戏开始后生成僵尸
    /// </summary>
    public class ZombieSpawnSystem : ISystem
    {
        public Fix64 zombieMoveSpeed = (Fix64)0.03f;
        public void Execute(World world, List<FrameData> inputs)
        {
            // 检查是否已经有僵尸存在（避免重复生成）
            var existingZombies = world.GetEntitiesWithComponent<ZombieAIComponent>();
            if (existingZombies.Count()> 0)
                return; // 已经有僵尸了，不重复生成

            for (int i = 0; i < 2; i++)
            {
                Entity zombieEntity = world.CreateEntity();

                // 设置僵尸初始位置（在地图右上角）
                FixVector2 zombiePosition = new FixVector2((Fix64)(10+i), (Fix64)6);
                Fix64 nearestDis = Fix64.MaxValue;
                FixVector2 nearestPosition = FixVector2.Zero;
                foreach (var (_,_,transform) in world.GetEntitiesWithComponents<PlayerComponent,Transform2DComponent>())
                {
                    if ((transform.position - zombiePosition).SqrMagnitude() < nearestDis)
                    {
                        nearestPosition = transform.position;
                    }
                }
                // 生成一个僵尸
           

                // 添加必要的组件
                var transformZ = new Transform2DComponent(zombiePosition);
                var zombieAI = new ZombieAIComponent(nearestPosition, zombieMoveSpeed);
          
                
                var physicsBody = new PhysicsBodyComponent(
                    Fix64.One, 
                    false, 
                    false, 
                    false,
                    Fix64.Zero,
                    Fix64.Zero, 
                    (Fix64)0.2,
                    (int)PhysicsLayer.Zombie
                );
                var collisionShape = CollisionShapeComponent.CreateCircle((Fix64)0.5);
                var velocity = new VelocityComponent();

                world.AddComponent(zombieEntity, transformZ);
                world.AddComponent(zombieEntity, zombieAI);
                world.AddComponent(zombieEntity, physicsBody);
                world.AddComponent(zombieEntity, collisionShape);
                world.AddComponent(zombieEntity, velocity);

                UnityEngine.Debug.Log($"[ZombieSpawnSystem] Spawned zombie at {zombiePosition}");
            }
       
        }
    }
}

