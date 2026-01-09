using System.Collections.Generic;
using Frame.FixMath;
using Proto;

namespace Frame.ECS
{
    public class PlayerMoveSystem:ISystem
    {
        
        public static Fix64 PlayerSpeed = (Fix64)0.1f;
        
        public void Execute(World world, List<FrameData> inputs)
        {
            foreach (var frameData in inputs)
            {
                var (playerId, inputDirection)  = (frameData.PlayerId,frameData.Direction);
                // 跳过无输入
                if (inputDirection == InputDirection.DirectionNone)
                    continue;

                // 查找玩家的Entity（通过PlayerComponent的playerId）
                Entity? playerEntity = FindPlayerEntity(world, playerId);
                if (!playerEntity.HasValue)
                    continue;

                // 获取PlayerComponent
                if (!world.TryGetComponent<PlayerComponent>(playerEntity.Value, out var playerComponent))
                    continue;

                // 将输入方向转换为移动向量
                FixVector2 movementDirection = Util.GetMovementDirection(inputDirection);

                // 更新玩家位置
                FixVector2 newPosition = playerComponent.position + movementDirection * PlayerSpeed;

                // 更新PlayerComponent
                var updatedComponent = new PlayerComponent(
                    playerComponent.playerId,
                    newPosition,
                    playerComponent.hp
                );
                world.AddComponent(playerEntity.Value, updatedComponent);
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