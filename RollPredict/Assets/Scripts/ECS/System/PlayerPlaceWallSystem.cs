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

                // 获取PlayerComponent并检查模式
                if (!world.TryGetComponent<PlayerComponent>(playerEntity.Value, out var playerComponent))
                    continue;

                // 检查僵直状态（僵直状态下无法操作）
                if (world.TryGetComponent<StiffComponent>(playerEntity.Value, out var stiff))
                {
                    if (stiff.IsStiff)
                    {
                        continue; // 僵直状态，无法放置墙
                    }
                }

                // 检查是否在放置墙模式（currentIndex == 0 表示放置墙模式）
                if (playerComponent.currentIndex != 0)
                {
                    continue; // 不是放置墙模式
                }

                // 检查墙冷却
                if (playerComponent.wallCooldownTimer > Fix64.Zero)
                {
                    continue; // 冷却中，不允许放置
                }

                PlaceWall(world, playerEntity.Value, playerComponent);
            }
        }

        /// <summary>
        /// 放置墙  WallComponent() Transform2DComponent() PhysicsBodyComponent() CollisionShapeComponent() VelocityComponent  HPComponent WallPlacementComponent
        /// </summary>
        private void PlaceWall(World world, Entity playerEntity, PlayerComponent playerComponent)
        {
            // 获取玩家位置
            if (!world.TryGetComponent<Transform2DComponent>(playerEntity, out var playerTransform))
                return;

            // 获取地图组件（每次调用都重新获取，确保使用最新状态）
            Entity? mapEntity = null;
            GridMapComponent? map = null;
            foreach (var (_mapEntity, _map) in world.GetEntitiesWithComponents<GridMapComponent>())
            {
                mapEntity = _mapEntity;
                map = _map;
                break;
            }

            if (!mapEntity.HasValue || !map.HasValue)
                return; // 地图不存在，无法放置墙

            FixVector2 targetPosition = playerTransform.position;

            // 将目标位置对齐到网格中心
            GridNode targetGrid = map.Value.WorldToGrid(targetPosition);
            FixVector2 alignedPosition = map.Value.GridToWorld(targetGrid);

            // 检查该网格是否已经有障碍物（避免重复放置）
            // 注意：必须使用最新的地图状态，而不是传入的副本
            if (!map.Value.IsWalkable(targetGrid))
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
            // 初始状态：isTrigger = true（不阻挡，等待放置者离开）
            var physicsBodyComponent = new PhysicsBodyComponent(
                Fix64.Zero, // mass = 0（static）
                isStatic: true, // 静态物体
                useGravity: false,
                isTrigger: true, // 初始为trigger，不阻挡放置者
                restitution: Fix64.Zero,
                friction: Fix64.Zero,
                linearDamping: Fix64.Zero,
                (int)PhysicsLayer.Wall
            );

            // 添加碰撞形状（方块，大小与网格相同）
            var collisionShapeComponent = CollisionShapeComponent.CreateBox(
                map.Value.cellSize, // 宽度 = 网格大小
                map.Value.cellSize // 高度 = 网格大小
            );

            // 添加速度组件（虽然static不需要，但为了兼容性）
            var velocityComponent = new VelocityComponent();
            
            // 添加血量组件（墙可以被破坏）
            var hpComponent = new HPComponent(50); // 墙初始血量50

            // 添加墙放置组件（标记正在放置中，等待放置者离开）
            var wallPlacementComponent = new WallPlacementComponent(playerEntity.Id);

            // 添加所有组件到实体
            world.AddComponent(wallEntity, wallComponent);
            world.AddComponent(wallEntity, transform2DComponent);
            world.AddComponent(wallEntity, physicsBodyComponent);
            world.AddComponent(wallEntity, collisionShapeComponent);
            world.AddComponent(wallEntity, velocityComponent);
            world.AddComponent(wallEntity, hpComponent);
            world.AddComponent(wallEntity, wallPlacementComponent); // 标记正在放置中

            // 将墙的位置添加到地图的障碍物列表
            // 关键：必须重新获取地图组件，确保使用最新状态（包括之前放置的墙）
            // 然后更新地图组件，确保后续的墙放置能正确检查障碍物
            var updatedMap = map.Value;
            updatedMap.obstacles.Add(targetGrid);
            world.AddComponent(mapEntity.Value, updatedMap);

            // 应用墙冷却
            var updatedPlayer = playerComponent;
            updatedPlayer.wallCooldownTimer = PlayerComponent.WallCooldownDuration;
            world.AddComponent(playerEntity, updatedPlayer);
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