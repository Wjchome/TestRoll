using System.Collections.Generic;
using Frame.FixMath;
using Proto;

namespace Frame.ECS
{
    /// <summary>
    /// 玩家放置墙系统：处理玩家放置墙的逻辑
    /// </summary>
    public class PlayerPlaceWallSystem : ISystem
    {
        public void Execute(World world, List<FrameData> inputs)
        {
            // 获取地图组件（用于网格对齐）
            var mapEntity = world.GetOrCreateSingleton(() => new GridMapComponent(20, 20, Fix64.One));
            if (!world.TryGetComponent<GridMapComponent>(mapEntity, out var map))
            {
                return; // 理论上不会发生，但为了安全
            }

            foreach (var frameData in inputs)
            {
                // 检查是否发射（在放置墙模式下，IsFire 表示放置墙）
                if (!frameData.IsFire)
                    continue;

                var playerId = frameData.PlayerId;

                // 查找玩家的Entity
                Entity? playerEntity = FindPlayerEntity(world, playerId);
                if (!playerEntity.HasValue)
                    continue;

                // 检查玩家是否在放置墙模式（currentIndex == 0 表示放置墙模式）
                if (world.TryGetComponent<PlayerComponent>(playerEntity.Value, out var p))
                {
                    if (p.currentIndex == 0)
                    {
                        continue;
                        // 放置墙模式
                    }
                } 
                PlaceWall(world,  mapEntity,map,  playerEntity.Value);
                
            }
        }

        /// <summary>
        /// 放置墙
        /// </summary>
        private void PlaceWall(World world, Entity mapEntity,GridMapComponent map,  Entity playerEntity)
        {
            // 获取玩家位置
            if (!world.TryGetComponent<Transform2DComponent>(playerEntity, out var playerTransform))
                return;

            // 获取目标位置（从 FireX, FireY）
            FixVector2 targetPosition = playerTransform.position;

            // 将目标位置对齐到网格中心
            GridNode targetGrid = map.WorldToGrid(targetPosition);
            FixVector2 alignedPosition = map.GridToWorld(targetGrid);

            // 检查该网格是否已经有障碍物（避免重复放置）
            if (!map.IsWalkable(targetGrid))
            {
                // 该位置已经有障碍物，不放置
                return;
            }

            // 创建墙实体
            Entity wallEntity = world.CreateEntity();

            // 添加墙组件
            var wallComponent = new WallComponent();
            
            // 添加位置组件（对齐到网格中心）
            var transform2DComponent = new Transform2DComponent(alignedPosition);
            
            // 添加物理组件（static，不移动，不响应重力）
            var physicsBodyComponent = new PhysicsBodyComponent(
                Fix64.Zero,      // mass = 0（static）
                isStatic: true,   // 静态物体
                useGravity: false,
                isTrigger: false,
                restitution: Fix64.Zero,
                friction: Fix64.Zero,
                linearDamping: Fix64.Zero
            );
            
            // 添加碰撞形状（方块，大小与网格相同）
            var collisionShapeComponent = CollisionShapeComponent.CreateBox(
                map.cellSize,  // 宽度 = 网格大小
                map.cellSize   // 高度 = 网格大小
            );
            
            // 添加速度组件（虽然static不需要，但为了兼容性）
            var velocityComponent = new VelocityComponent();

            // 添加所有组件到实体
            world.AddComponent(wallEntity, wallComponent);
            world.AddComponent(wallEntity, transform2DComponent);
            world.AddComponent(wallEntity, physicsBodyComponent);
            world.AddComponent(wallEntity, collisionShapeComponent);
            world.AddComponent(wallEntity, velocityComponent);

            // 将墙的位置添加到地图的障碍物列表
            // 注意：需要更新地图组件（因为它是单例组件）
            map.obstacles.Add(targetGrid);
            world.AddComponent(mapEntity, map);
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