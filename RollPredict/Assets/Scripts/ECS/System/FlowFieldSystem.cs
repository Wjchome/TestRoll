using System.Collections.Generic;
using Frame.FixMath;
using Proto;

namespace Frame.ECS
{
    public class FlowFieldSystem:ISystem
    {
        public const int flowTime = 10;
        public void Execute(World world, List<FrameData> inputs)
        {
            var playerPositions = new List<FixVector2>();
            foreach (var (playerEntity, playerTransform, _) in world
                         .GetEntitiesWithComponents<Transform2DComponent, PlayerComponent>())
            {
                playerPositions.Add(playerTransform.position);
            }
            foreach (var (entity,flowFieldComponent) in world.GetEntitiesWithComponents<FlowFieldComponent>())
            {
                
                foreach (var (_, gridMapComponent) in world.GetEntitiesWithComponents<GridMapComponent>())
                {
                    var newFlow = flowFieldComponent;
                    newFlow.updateCooldown--;
                    if (newFlow.updateCooldown <= 0)
                    {
                        newFlow.gradientField = FlowFieldPathfinding.ComputeFlowField(gridMapComponent,playerPositions);
                        newFlow.updateCooldown = flowTime;
                    }
                    world.AddComponent(entity, newFlow);
                }
                
            }
        }

    }
}