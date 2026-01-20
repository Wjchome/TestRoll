using System.Collections.Generic;
using Frame.FixMath;
using Proto;

namespace Frame.ECS
{
    /// <summary>
    /// 死亡系统：统一处理所有实体的死亡逻辑
    /// 
    /// 功能：
    /// - 处理所有有 DeathComponent 的实体
    /// - 根据实体类型执行不同的死亡逻辑
    ///   - 玩家死亡：触发游戏结束等
    ///   - 僵尸死亡：掉落物品、增加分数等
    ///   - 墙被摧毁：从地图障碍物中移除
    /// - 统一销毁死亡实体
    /// 
    /// 设计说明：
    /// - 使用 DeathComponent 作为标记，只处理有标记的实体（性能优化）
    /// - 所有死亡逻辑集中在一个System，职责清晰
    /// - 易于扩展：新增实体类型只需添加处理方法
    /// </summary>
    public class DeathSystem : ISystem
    {
        public void Execute(World world, List<FrameData> inputs)
        {
            List<Entity> entitiesToDestroy = new List<Entity>();
            List<Entity> entitiesToDestro = new List<Entity>();

            // 处理所有有 DeathComponent 的实体
            foreach (var (entity, death) in world.GetEntitiesWithComponents<DeathComponent>())
            {
                // 根据实体类型执行不同的死亡逻辑
                if (world.TryGetComponent<PlayerComponent>(entity, out var player))
                {
                    HandlePlayerDeath(world, entity);
                }
                else if (world.TryGetComponent<ZombieAIComponent>(entity, out var zombie))
                {
                    HandleZombieDeath(world, entity);
                    entitiesToDestroy.Add(entity);
                    
                }
                else if (world.TryGetComponent<WallComponent>(entity, out var wall))
                {
                    HandleWallDeath(world, entity);
                    entitiesToDestroy.Add(entity);
                }
                else if (world.TryGetComponent<BarrelComponent>(entity, out var barrel))
                {
                    HandleBarrelDeath(world, entity);
                    entitiesToDestro.Add(entity);
                }
            }

            // 统一销毁死亡实体
            foreach (var entity in entitiesToDestroy)
            {
                world.DestroyEntity(entity);
            }
            foreach (var entity in entitiesToDestro)
            {
                world.RemoveComponent<DeathComponent>(entity);
                
            }
        }

        /// <summary>
        /// 处理玩家死亡
        /// </summary>
        private void HandlePlayerDeath(World world, Entity entity)
        {
            // 玩家死亡逻辑
            // - 触发游戏结束？
            // - 播放死亡动画？
            // - 移除玩家相关数据？

            // 暂时只记录日志
            UnityEngine.Debug.Log($"[DeathSystem] Player {entity.Id} died");
        }

        /// <summary>
        /// 处理僵尸死亡
        /// </summary>
        private void HandleZombieDeath(World world, Entity entity)
        {
            // 僵尸死亡逻辑
            // - 掉落物品？
            // - 增加分数？
            // - 播放死亡动画？

            // 暂时只记录日志
            UnityEngine.Debug.Log($"[DeathSystem] Zombie {entity.Id} died");
        }

        /// <summary>
        /// 处理墙被摧毁
        /// </summary>
        private void HandleWallDeath(World world, Entity entity)
        {
            // 从地图障碍物中移除墙的位置
            RemoveWallFromMap(world, entity);
            UnityEngine.Debug.Log($"[DeathSystem] Wall {entity.Id} destroyed");
        }
        
        
        private void HandleBarrelDeath(World world, Entity entity)
        {
            // 从地图障碍物中移除墙的位置
            RemoveWallFromMap(world, entity);
            if (world.TryGetComponent<Transform2DComponent>(entity, out var transform))
            {
                world.AddComponent(entity, new  ExplosionComponent (transform.position,(Fix64)2,10));
            }
            
        }

        /// <summary>
        /// 从地图障碍物中移除墙（当墙被摧毁时调用）
        /// </summary>
        private void RemoveWallFromMap(World world, Entity wallEntity)
        {
            // 获取墙的位置
            if (!world.TryGetComponent<Transform2DComponent>(wallEntity, out var wallTransform))
                return;

            // 获取地图组件
            foreach (var (mapEntity, map) in world.GetEntitiesWithComponents<GridMapComponent>())
            {
                // 将墙的世界坐标转换为网格坐标
                GridNode wallGrid = map.WorldToGrid(wallTransform.position);

                // 从障碍物列表中移除
                var updatedMap = map;
                updatedMap.obstacles.Remove(wallGrid);
                world.AddComponent(mapEntity, updatedMap);

                break; // 只处理第一个地图
            }
        }
    }
}