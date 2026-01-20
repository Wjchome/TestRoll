using System.Collections.Generic;
using Frame.FixMath;
using Proto;

namespace Frame.ECS
{
    /// <summary>
    /// 玩家冷却系统：每帧更新玩家的冷却时间
    /// </summary>
    public class PlayerCooldownSystem : ISystem
    {
        public void Execute(World world, List<FrameData> inputs)
        {
            // 每帧减少冷却时间（与物理系统保持一致，每帧 = Fix64.One）
            // 注意：在帧同步游戏中，每帧时间应该是固定的，确保所有客户端同步
            Fix64 deltaTime = Fix64.One; // 每帧固定时间单位
            
            foreach (var entity in world.GetEntitiesWithComponent<PlayerComponent>())
            {
                if (!world.TryGetComponent<PlayerComponent>(entity, out var playerComponent))
                    continue;
                
                var updatedPlayer = playerComponent;
                
                // 减少子弹冷却时间
                if (updatedPlayer.bulletCooldownTimer > Fix64.Zero)
                {
                    updatedPlayer.bulletCooldownTimer -= deltaTime;
                    if (updatedPlayer.bulletCooldownTimer < Fix64.Zero)
                    {
                        updatedPlayer.bulletCooldownTimer = Fix64.Zero;
                    }
                }
                
                // 减少墙冷却时间
                if (updatedPlayer.wallCooldownTimer > Fix64.Zero)
                {
                    updatedPlayer.wallCooldownTimer -= deltaTime;
                    if (updatedPlayer.wallCooldownTimer < Fix64.Zero)
                    {
                        updatedPlayer.wallCooldownTimer = Fix64.Zero;
                    }
                }
                
                // 减少油桶冷却时间
                if (updatedPlayer.barrelCooldownTimer > Fix64.Zero)
                {
                    updatedPlayer.barrelCooldownTimer -= deltaTime;
                    if (updatedPlayer.barrelCooldownTimer < Fix64.Zero)
                    {
                        updatedPlayer.barrelCooldownTimer = Fix64.Zero;
                    }
                }
                
                world.AddComponent(entity, updatedPlayer);
            }
        }
    }
}

