using System.Collections.Generic;
using Frame.FixMath;
using Proto;

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

                // 获取PlayerComponent
                if (!world.TryGetComponent<PlayerComponent>(playerEntity.Value, out var playerComponent))
                    continue;

                // 计算子弹方向（从玩家位置指向目标位置）
                FixVector2 bulletPosition = playerComponent.position;
                FixVector2 targetPosition =
                    new FixVector2(Fix64.FromRaw(frameData.FireX), Fix64.FromRaw(frameData.FireY));

                // 计算方向向量并归一化
                FixVector2 direction = targetPosition - bulletPosition;
                Fix64 distance = Fix64.Sqrt(direction.x * direction.x + direction.y * direction.y);

                if (distance > Fix64.Zero)
                {
                    direction.Normalize();
                    FixVector2 bulletVelocity = direction * BulletSpeed;

                    // 创建子弹Entity
                    // 注意：子弹不再需要单独的bulletId，Entity.Id本身就是唯一标识
                    Entity bulletEntity = world.CreateEntity();
                    var bulletComponent = new BulletComponent(
                        bulletPosition,
                        bulletVelocity,
                        playerEntity.Value.Id
                    );
                    world.AddComponent(bulletEntity, bulletComponent);
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