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

            var map = new GridMapComponent();

            foreach (var (mapEntity,_map) in world.GetEntitiesWithComponents<GridMapComponent>())
            {
                map =  _map;
            }

            

            // 获取所有玩家位置（用于寻找最近玩家）
            var playerPositions = new List<FixVector2>();
            foreach (var (playerEntity, playerTransform, _) in world
                         .GetEntitiesWithComponents<Transform2DComponent, PlayerComponent>())
            {
                playerPositions.Add(playerTransform.position);
            }

            // 获取所有僵尸
            foreach (var (entity, transform, ai,velocityComponent) in
                     world.GetEntitiesWithComponents<Transform2DComponent, ZombieAIComponent,VelocityComponent>())
            {
                var updatedAI = ai;

                // 更新寻路冷却
                if (ai.pathfindingCooldown > 0)
                {
                    updatedAI.pathfindingCooldown--;
                    world.AddComponent(entity, updatedAI);
                }
                else
                {
                    // 寻找最近的玩家
                    FixVector2 nearestPlayerPos = FixVector2.Zero;
                    Fix64 minDistance = Fix64.MaxValue;

                    foreach (var playerPos in playerPositions)
                    {
                        FixVector2 diff = playerPos - transform.position;
                        Fix64 distance = Fix64.Sqrt(diff.x * diff.x + diff.y * diff.y);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            nearestPlayerPos = playerPos;
                        }
                    }

                    // 如果没有找到玩家，跳过
                    if (playerPositions.Count == 0)
                        continue;


                    updatedAI.targetPosition = nearestPlayerPos;
                    updatedAI.currentPath = null;
                    updatedAI.currentPathIndex = 0;


                    var path = DeterministicPathfinding.FindPath(map, transform.position, updatedAI.targetPosition);

                    if (path != null && path.Count > 0)
                    {
                        updatedAI.currentPath = path;
                        updatedAI.currentPathIndex = 0;
                        updatedAI.pathfindingCooldown = PATHFINDING_COOLDOWN_FRAMES;
                    }
                    else
                    {
                        // 寻路失败，清空路径
                        updatedAI.currentPath = null;
                        updatedAI.currentPathIndex = 0;
                    }

                    world.AddComponent(entity, updatedAI);
                }


                // 沿着路径移动（需要重新获取AI组件，因为可能被更新了）
                if (!world.TryGetComponent<ZombieAIComponent>(entity, out var currentAI))
                    continue;

                if (currentAI.currentPath != null && currentAI.currentPath.Count > 0 &&
                    currentAI.currentPathIndex < currentAI.currentPath.Count)
                {
                    FixVector2 targetPoint = currentAI.currentPath[currentAI.currentPathIndex];
                    FixVector2 direction = targetPoint - transform.position;
                    Fix64 distance = Fix64.Sqrt(direction.x * direction.x + direction.y * direction.y);

                    // 如果到达当前路径点，移动到下一个点
                    if (distance < map.cellSize / Fix64.Two)
                    {
                        var _updatedAI = currentAI;
                        _updatedAI.currentPathIndex++;
                        world.AddComponent(entity, _updatedAI);
                    }
                    else
                    {
                        // 移动到目标点
                        direction.Normalize();
                        var newVelocity = velocityComponent;
                        newVelocity.velocity += direction * currentAI.moveSpeed;
       
                        world.AddComponent(entity, newVelocity);
                    }
                }
            }
        }
    }
}