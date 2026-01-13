using System.Collections.Generic;
using System.Linq;
using Frame.FixMath;
using Proto;

namespace Frame.ECS
{
    /// <summary>
    /// 僵尸AI系统：处理僵尸的寻路和移动
    /// </summary>
    public class ZombieAISystem : ISystem
    {
        // 寻路冷却时间（帧数）
        private const int PATHFINDING_COOLDOWN_FRAMES = 10;

        public void Execute(World world, List<FrameData> inputs)
        {
            // 获取地图单例组件（如果不存在会自动创建）
            var mapEntity = world.GetOrCreateSingleton(() => new GridMapComponent(20, 20, Fix64.One));
            if (!world.TryGetComponent<GridMapComponent>(mapEntity, out var map))
            {
                return; // 理论上不会发生，但为了安全
            }

            // 获取所有僵尸
            foreach (var (entity, transform, ai) in
                     world.GetEntitiesWithComponents<Transform2DComponent, ZombieAIComponent>())
            {
                // 更新寻路冷却
                if (ai.pathfindingCooldown > 0)
                {
                    var updatedAI = ai;
                    updatedAI.pathfindingCooldown--;
                    world.AddComponent(entity, updatedAI);
                    continue;
                }


                var path = DeterministicPathfinding.FindPath(map, transform.position, ai.targetPosition);

                if (path != null && path.Count > 0)
                {
                    var updatedAI = ai;
                    updatedAI.currentPath = path;
                    updatedAI.currentPathIndex = 0;
                    updatedAI.pathfindingCooldown = PATHFINDING_COOLDOWN_FRAMES;
                    world.AddComponent(entity, updatedAI);
                }

                // 沿着路径移动
                if (ai.currentPath != null && ai.currentPath.Count > 0 &&
                    ai.currentPathIndex < ai.currentPath.Count)
                {
                    FixVector2 targetPoint = ai.currentPath[ai.currentPathIndex];
                    FixVector2 direction = targetPoint - transform.position;
                    Fix64 distance = Fix64.Sqrt(direction.x * direction.x + direction.y * direction.y);

                    // 如果到达当前路径点，移动到下一个点
                    if (distance < map.cellSize / Fix64.Two)
                    {
                        var updatedAI = ai;
                        updatedAI.currentPathIndex++;
                        world.AddComponent(entity, updatedAI);
                    }
                    else
                    {
                        // 移动到目标点
                        direction.Normalize();
                        FixVector2 velocity = direction * ai.moveSpeed;

                        // 更新位置
                        var updatedTransform = transform;
                        updatedTransform.position += velocity;
                        world.AddComponent(entity, updatedTransform);
                    }
                }
            }
        }
    }
}