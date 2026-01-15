using System.Collections.Generic;
using Frame.FixMath;
using Proto;

namespace Frame.ECS
{
    /// <summary>
    /// 玩家状态系统：处理玩家状态机
    /// 
    /// 功能：
    /// - 处理受伤僵直状态
    /// - 在僵直期间限制玩家移动/操作（可选）
    /// </summary>
    public class PlayerStateSystem : ISystem
    {
        public void Execute(World world, List<FrameData> inputs)
        {
            foreach (var (entity, player) in world.GetEntitiesWithComponents<PlayerComponent>())
            {
                var updatedPlayer = player;

                switch (player.state)
                {
                    case PlayerState.Normal:
                        // 正常状态，无需处理
                        break;

                    case PlayerState.HitStun:
                        // 受伤僵直状态：计时器递减
                        updatedPlayer.hitStunTimer--;
                        if (updatedPlayer.hitStunTimer <= 0)
                        {
                            // 僵直结束，回到正常状态
                            updatedPlayer.state = PlayerState.Normal;
                            updatedPlayer.hitStunTimer = 0;
                        }
                        world.AddComponent(entity, updatedPlayer);
                        break;
                }
            }
        }
    }
}

