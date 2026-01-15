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

                // 检查僵直状态（僵直状态下无法移动）
                if (world.TryGetComponent<StiffComponent>(playerEntity.Value, out var stiff))
                {
                    if (stiff.IsStiff)
                    {
                        // 僵直状态下，忽略移动输入
                        continue;
                    }
                }
                
                // 获取VelocityComponent
                if (!world.TryGetComponent<VelocityComponent>(playerEntity.Value, out var velocityComponent))
                    continue;
                
                // 将输入方向转换为移动向量
                FixVector2 movementDirection = Util.GetMovementDirection(inputDirection);

                AddForceHelper.ApplyForce(world,playerEntity.Value,movementDirection * PlayerSpeed);
                
                // // 更新玩家位置
                // velocityComponent.velocity += movementDirection * PlayerSpeed;
                //
                // world.AddComponent(playerEntity.Value, velocityComponent);
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