using System.Collections.Generic;
using Frame.FixMath;
using Proto;
using UnityEngine;

namespace Frame.ECS
{
    /// <summary>
    /// 血量伤害辅助类：统一处理所有实体的伤害
    /// 
    /// 功能：
    /// - 对任何有HPComponent的实体造成伤害
    /// - 触发受伤僵直状态（如果有StiffComponent）
    /// - 处理实体死亡（后续实现）
    /// 
    /// 注意：这是一个辅助类，不是 System，由其他 System 调用
    /// </summary>
    public static class HPDamageHelper
    {
        /// <summary>
        /// 对实体造成伤害（通用方法，适用于玩家、僵尸、Boss等）
        /// </summary>
        /// <param name="world">World实例</param>
        /// <param name="entity">目标Entity</param>
        /// <param name="damage">伤害值</param>
        public static void ApplyDamage(World world, Entity entity, int damage)
        {
            // 1. 处理血量
            if (world.TryGetComponent<HPComponent>(entity, out var hp))
            {
                var updatedHP = hp;
                updatedHP.HP = System.Math.Max(0, updatedHP.HP - damage);
                world.AddComponent(entity, updatedHP);
                
                // 2. 如果血量 <= 0，添加死亡标记（由DeathSystem统一处理）
                if (updatedHP.HP <= 0)
                {
                    world.AddComponent(entity, new DeathComponent());
                }
                // 3. 如果受到伤害且未死亡，触发僵直状态
                else if (damage > 0)
                {
                    if (world.TryGetComponent<StiffComponent>(entity, out var stiff))
                    {
                        var updatedStiff = stiff;
                        updatedStiff.stiffTimer = updatedStiff.stiffDuration; // 触发僵直
                        world.AddComponent(entity, updatedStiff);
                    }
                }
            }
        }
    }
}

