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
        private Fix64 AttackSqr = (Fix64)0.5f;

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
                var newVelocity = velocityComponent;
                
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
                
                // 更新寻路冷却
                if (ai.pathfindingCooldown > 0)
                {
                    updatedAI.pathfindingCooldown--;
                    world.AddComponent(entity, updatedAI);
                }
                else
                {
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

                

                if (updatedAI.currentPath != null && updatedAI.currentPath.Count > 0 &&
                    updatedAI.currentPathIndex < updatedAI.currentPath.Count)
                {
                    FixVector2 targetPoint = updatedAI.currentPath[updatedAI.currentPathIndex];
                    FixVector2 direction = targetPoint - transform.position;
                    Fix64 distance = Fix64.Sqrt(direction.x * direction.x + direction.y * direction.y);

                    // 如果到达当前路径点，移动到下一个点
                    if (distance < map.cellSize / Fix64.Two)
                    {
                        world.AddComponent(entity, updatedAI);
                    }
                    else
                    {
                        // 移动到目标点
                        direction.Normalize();
                        newVelocity.velocity += direction * updatedAI.moveSpeed;
                        world.AddComponent(entity, newVelocity);
                    }
                }

                
            }
        }
    }
}