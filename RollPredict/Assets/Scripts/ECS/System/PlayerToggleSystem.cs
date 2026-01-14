using System.Collections.Generic;
using Frame.FixMath;
using Proto;
using UnityEngine;

namespace Frame.ECS
{
    public class PlayerToggleSystem : ISystem
    {
        public void Execute(World world, List<FrameData> inputs)
        {
            foreach (var frameData in inputs)
            {
                var (playerId, isToggle) = (frameData.PlayerId, frameData.IsToggle);
                if (!isToggle)
                {
                    continue;
                }
                // 查找玩家的Entity（通过PlayerComponent的playerId）
                Entity? playerEntity = FindPlayerEntity(world, playerId);
                if (!playerEntity.HasValue)
                    continue;

                if (world.TryGetComponent<PlayerComponent>(playerEntity.Value, out var p))
                {
                    p.currentIndex = (p.currentIndex +  1 ) % p.sumIndex;
                }
                world.AddComponent(playerEntity.Value,p);
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