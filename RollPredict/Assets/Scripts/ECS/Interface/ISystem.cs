using System.Collections.Generic;
using Proto;

namespace Frame.ECS
{
    public interface ISystem
    {
        void Execute(World world, List<FrameData> inputs);
        
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