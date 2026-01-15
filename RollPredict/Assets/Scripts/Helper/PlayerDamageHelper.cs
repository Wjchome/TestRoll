using System.Collections.Generic;
using Frame.FixMath;
using Proto;

namespace Frame.ECS
{
    /// <summary>
    /// 玩家伤害系统：统一处理玩家受到的伤害
    /// 
    /// 功能：
    /// - 对玩家造成伤害
    /// - 触发受伤僵直状态
    /// - 处理玩家死亡（后续实现）
    /// </summary>
    public static class PlayerDamageHelper
    {
        /// <summary>
        /// 对玩家造成伤害
        /// </summary>
        /// <param name="world">World实例</param>
        /// <param name="playerEntity">玩家Entity</param>
        /// <param name="damage">伤害值</param>
        public static void ApplyDamage(World world, Entity playerEntity, int damage)
        {
            if (!world.TryGetComponent<PlayerComponent>(playerEntity, out var player))
                return;

            var updatedPlayer = player;
            
            // 1. 减少血量
            updatedPlayer.HP = System.Math.Max(0, updatedPlayer.HP - damage);
            
            // 2. 如果受到伤害，进入受伤僵直状态
            if (damage > 0 && updatedPlayer.HP > 0)
            {
                updatedPlayer.state = PlayerState.HitStun;
                updatedPlayer.hitStunTimer = PlayerComponent.HitStunDuration;
            }
            
            world.AddComponent(playerEntity, updatedPlayer);

            // 3. 如果血量 <= 0，触发死亡事件（后续实现）
            // if (updatedPlayer.HP <= 0)
            // {
            //     HandlePlayerDeath(world, playerEntity);
            // }
        }
    }
}

