using System.Collections.Generic;
using Frame.FixMath;
using Frame.Physics2D;
using Proto;

namespace Frame.ECS
{
    /// <summary>
    /// 墙放置系统：处理墙的放置状态转换
    /// 
    /// 功能：
    /// - 检查正在放置的墙（有WallPlacementComponent）
    /// - 检测放置者是否还在墙的范围内
    /// - 如果放置者离开，将墙从trigger状态转换为正常碰撞状态
    /// 
    /// 设计说明：
    /// - 墙刚放置时是trigger（不阻挡），放置者可以自由移动
    /// - 放置者离开后，墙变为正常碰撞（阻挡所有实体）
    /// - 这是很多游戏的标准做法，避免放置时卡住自己
    /// </summary>
    public class WallPlacementSystem : ISystem
    {
        public void Execute(World world, List<FrameData> inputs)
        {
            var wallEntites = new List<Entity>();
            // 处理所有正在放置的墙
            foreach (var (wallEntity, placement, wallTransform, wallShape) in world
                         .GetEntitiesWithComponents<WallPlacementComponent, Transform2DComponent, CollisionShapeComponent>())
            {
                // 检查放置者是否还存在
                Entity placerEntity = new Entity(placement.placerEntityId);
                if (!world.HasComponent<Transform2DComponent>(placerEntity))
                {
                    // 放置者已不存在（可能被销毁），直接激活墙
                    ActivateWall(world, wallEntity);
                    wallEntites.Add(wallEntity);
                    
                    continue;
                }

                // 获取放置者位置
                if (!world.TryGetComponent<Transform2DComponent>(placerEntity, out var placerTransform))
                {
                    ActivateWall(world, wallEntity);
                    wallEntites.Add(wallEntity);
                    
                    continue;
                }

                // 检查放置者是否还在墙的范围内
                // 使用墙的AABB边界，稍微扩大一点作为检测范围
                FixRect wallBounds = wallShape.GetBounds(wallTransform.position);
                
                // 扩大检测范围（增加一点容差，避免边界情况）
                Fix64 margin = (Fix64)0.1;
                FixRect expandedBounds = new FixRect(
                    wallBounds.X - margin,
                    wallBounds.Y - margin,
                    wallBounds.Width + margin * Fix64.Two,
                    wallBounds.Height + margin * Fix64.Two
                );

                // 检查放置者是否在范围内（手动实现点是否在矩形内）
                bool isInside = placerTransform.position.x >= expandedBounds.X &&
                               placerTransform.position.x <= expandedBounds.Right &&
                               placerTransform.position.y >= expandedBounds.Y &&
                               placerTransform.position.y <= expandedBounds.Top;

                if (!isInside)
                {
                    // 放置者已离开，激活墙（变为非trigger）
                    ActivateWall(world, wallEntity);
                    wallEntites.Add(wallEntity);
                    
                }
            }

            foreach (var wallEntity in wallEntites)
            {
                // 移除WallPlacementComponent（墙已激活，不再需要此组件）
                world.RemoveComponent<WallPlacementComponent>(wallEntity);
            }
        }

        /// <summary>
        /// 激活墙：将墙从trigger状态转换为正常碰撞状态
        /// </summary>
        private void ActivateWall(World world, Entity wallEntity)
        {
            // 更新PhysicsBodyComponent：将isTrigger设置为false
            if (world.TryGetComponent<PhysicsBodyComponent>(wallEntity, out var physicsBody))
            {
                var updatedBody = physicsBody;
                updatedBody.isTrigger = false; // 激活墙，开始阻挡
                world.AddComponent(wallEntity, updatedBody);
            }
            
        }
    }
}

