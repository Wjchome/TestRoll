using System.Collections.Generic;
using Frame.FixMath;
using Proto;
using UnityEngine;

namespace Frame.ECS
{
    public class PlayerPlaceWallSystem : ISystem
    {
        public static Fix64 BulletSpeed = (Fix64)0.2f;


        public void Execute(World world, List<FrameData> inputs)
        {
            foreach (var frameData in inputs)
            {
                if (!frameData.IsFire)
                    continue;

                var playerId = frameData.PlayerId;

                // 查找玩家的Entity
                Entity? playerEntity = FindPlayerEntity(world, playerId);
                if (!playerEntity.HasValue)
                    continue;
                if (world.TryGetComponent<PlayerComponent>(playerEntity.Value, out var p))
                {
                    if (p.currentIndex == 0)
                    {
                        continue;
                    }
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