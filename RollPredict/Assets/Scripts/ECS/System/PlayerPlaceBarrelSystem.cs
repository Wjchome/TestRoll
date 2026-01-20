using System.Collections.Generic;
using Frame.FixMath;
using Proto;
using UnityEngine;

namespace Frame.ECS
{
    /// <summary>
    /// 玩家放置油桶系统
    /// 
    /// 职责：
    /// - 检测玩家放置油桶的输入（currentIndex == 2 && 鼠标点击）
    /// - 在玩家位置创建油桶Entity
    /// - 添加所有必需的组件 BarrelComponent() Transform2DComponent() PhysicsBodyComponent() CollisionShapeComponent() VelocityComponent  HPComponent WallPlacementComponent
    /// - 应用冷却时间
    ///   
    /// 注意：
    /// - 油桶不像墙那样对齐网格，可以放置在任意位置
    /// - 油桶有物理组件，可以被推动（可选）
    /// - 油桶不需要WallPlacementComponent（不需要等待放置者离开）
    /// </summary>
    public class PlayerPlaceBarrelSystem : ISystem
    {
        public void Execute(World world, List<FrameData> inputs)
        {
            foreach (var input in inputs)
            {
                // 查找对应的玩家Entity
                Entity? playerEntity = FindPlayerEntity(world, input.PlayerId);
                if (!playerEntity.HasValue)
                    continue;

                // 获取玩家组件
                if (!world.TryGetComponent<PlayerComponent>(playerEntity.Value, out var playerComponent))
                    continue;

                // 检查是否是放置油桶模式（currentIndex == 2）
                if (playerComponent.currentIndex != 2)
                    continue;

                // 检查是否有放置输入（is_fire == true表示点击）
                if (!input.IsFire)
                    continue;

                // 检查冷却时间
                if (playerComponent.barrelCooldownTimer > Fix64.Zero)
                    continue; // 还在冷却中

                // 放置油桶
                PlaceBarrel(world, playerEntity.Value, playerComponent);
            }
        }

        /// <summary>
        /// 放置油桶
        /// </summary>
        private void PlaceBarrel(World world, Entity playerEntity, PlayerComponent playerComponent)
        {
            // 获取玩家位置（用于记录放置者）
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
            
            // 将目标位置对齐到网格中心
            GridNode targetGrid = map.Value.WorldToGrid(playerTransform.position);
            FixVector2 alignedPosition = map.Value.GridToWorld(targetGrid);

            // 创建油桶实体
            Entity barrelEntity = world.CreateEntity();

            // 添加油桶组件
            var barrelComponent =new BarrelComponent();

            // 添加位置组件（放置在目标位置）
            var transform2DComponent = new Transform2DComponent(alignedPosition);

            // 添加物理组件（静态，不移动，实心）
            var physicsBodyComponent = new PhysicsBodyComponent( Fix64.Zero,
                isStatic: true,     // 静态物体（可以改为动态，允许被推动）
                useGravity: false,  // 不受重力影响
                isTrigger: false,   // 实心，阻挡移动
                restitution: Fix64.Zero,
                friction: Fix64.Zero,
                linearDamping: Fix64.Zero,
                (int)PhysicsLayer.Barrel  // 使用墙的Layer（也可以创建Barrel Layer）
            );

            // 添加碰撞形状（圆形，半径0.4）
            var collisionShapeComponent = CollisionShapeComponent.CreateBox(
                map.Value.cellSize, // 宽度 = 网格大小
                map.Value.cellSize // 高度 = 网格大小
            );

            // 添加速度组件
            var velocityComponent = new VelocityComponent();

            // 添加血量组件（油桶有自己的血量）
            var hpComponent = new HPComponent(10);

            // 添加墙放置组件（标记正在放置中，等待放置者离开）
            var wallPlacementComponent = new WallPlacementComponent(playerEntity.Id);
            
            // 添加所有组件到实体
            world.AddComponent(barrelEntity, barrelComponent);
            world.AddComponent(barrelEntity, transform2DComponent);
            world.AddComponent(barrelEntity, physicsBodyComponent);
            world.AddComponent(barrelEntity, collisionShapeComponent);
            world.AddComponent(barrelEntity, velocityComponent);
            world.AddComponent(barrelEntity, hpComponent);
            world.AddComponent(barrelEntity, wallPlacementComponent);

            var updatedMap = map.Value;
            updatedMap.obstacles.Add(targetGrid);
            world.AddComponent(mapEntity.Value, updatedMap);
            
            // 应用油桶冷却
            var updatedPlayer = playerComponent;
            updatedPlayer.barrelCooldownTimer = PlayerComponent.BarrelCooldownDuration;
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



