using System;
using System.Collections.Generic;
using Frame.ECS;
using Frame.FixMath;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frame.ECS
{
    /// <summary>
    /// ECS同步辅助类：在ECS World和Unity对象之间同步状态
    /// 用于视图层显示
    /// 
    /// 优化点：
    /// 1. 提取通用同步逻辑，减少代码重复
    /// 2. 统一Entity类型管理，避免通过名称判断
    /// 3. 缓存常用引用，提升性能
    /// 4. 改进代码结构，提高可维护性
    /// </summary>
    public static class ECSSyncHelper
    {
        /// <summary>
        /// Entity到Unity对象的映射（视图层）
        /// Key: Entity ID
        /// Value: GameObject（Unity视图层）
        /// </summary>
        private static Dictionary<int, GameObject> _entityToGameObject = new Dictionary<int, GameObject>();

        /// <summary>
        /// playerId到Entity的映射
        /// Key: playerId（游戏逻辑层的ID）
        /// Value: Entity
        /// </summary>
        private static Dictionary<int, Entity> _playerIdToEntity = new Dictionary<int, Entity>();

        /// <summary>
        /// Entity到playerId的映射（反向查找）
        /// </summary>
        private static Dictionary<int, int> _entityToPlayerId = new Dictionary<int, int>();

        /// <summary>
        /// Entity类型标记（用于区分不同类型的Entity）
        /// Key: Entity ID
        /// Value: Entity类型（"Player", "Bullet", "Zombie"等）
        /// </summary>
        private static Dictionary<int, string> _entityTypeMap = new Dictionary<int, string>();



        /// <summary>
        /// 注册玩家到ECS系统
        /// </summary>
        public static Entity RegisterPlayer(World world, int playerId, GameObject gameObject,
            FixVector2 initialPosition, int initialHp)
        {
            // 创建Entity
            Entity entity = world.CreateEntity();

            // 添加PlayerComponent
            var playerComponent = new PlayerComponent(playerId, initialHp,2);
            var transform2DComponent = new Transform2DComponent(initialPosition);
            var physicsBodyComponent = new PhysicsBodyComponent(Fix64.One, false, false, false, Fix64.Zero,
                Fix64.Zero, (Fix64)0.5);
            var collisionShapeComponent = new CollisionShapeComponent(ShapeType.Box, Fix64.One, FixVector2.One);
            var velocityComponent = new VelocityComponent();
            world.AddComponent(entity, playerComponent);
            world.AddComponent(entity, transform2DComponent);
            world.AddComponent(entity, physicsBodyComponent);
            world.AddComponent(entity, collisionShapeComponent);
            world.AddComponent(entity, velocityComponent);

            // 建立映射
            _entityToGameObject[entity.Id] = gameObject;
            _playerIdToEntity[playerId] = entity;
            _entityToPlayerId[entity.Id] = playerId;
            _entityTypeMap[entity.Id] = "Player";

            return entity;
        }

        /// <summary>
        /// 从ECS World同步状态到Unity对象（用于视图层显示）
        /// World -> Unity
        /// </summary>
        public static void SyncFromWorldToUnity(World world)
        {
            // 同步玩家状态
            SyncEntities<PlayerComponent>(world, "Player", null, (entity, transform) =>
            {
                // 玩家已经通过RegisterPlayer注册，不需要创建GameObject
                return _entityToGameObject.TryGetValue(entity.Id, out var go) ? go : null;
            });

            // 同步子弹状态
            SyncEntities<BulletComponent>(world, "Bullet", ECSFrameSyncExample.Instance.bulletPrefab, null);

            // 同步僵尸状态
            SyncEntities<ZombieAIComponent>(world, "Zombie", ECSFrameSyncExample.Instance.zombiePrefab, null);
        }

        /// <summary>
        /// 通用Entity同步方法（泛型）
        /// 
        /// 处理流程：
        /// 1. 收集当前帧所有指定类型的Entity
        /// 2. 销毁ECS中不存在的GameObject
        /// 3. 创建或更新GameObject
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="world">ECS World</param>
        /// <param name="entityType">Entity类型标识（用于标记和过滤）</param>
        /// <param name="prefab">Prefab（如果为null，需要提供createGameObject回调）</param>
        /// <param name="getGameObject">获取GameObject的回调（用于玩家等已注册的Entity）</param>
        private static void SyncEntities<TComponent>(
            World world,
            string entityType,
            GameObject prefab,
            Func<Entity, Transform2DComponent, GameObject> getGameObject) where TComponent : struct, IComponent
        {
            // 1. 收集当前帧所有指定类型的Entity ID
            var currentEntityIds = new HashSet<int>();
            var entitiesWithTransform = new List<(Entity entity, Transform2DComponent transform)>();

            foreach (var entity in world.GetEntitiesWithComponent<TComponent>())
            {
                if (world.TryGetComponent<Transform2DComponent>(entity, out var transform))
                {
                    currentEntityIds.Add(entity.Id);
                    entitiesWithTransform.Add((entity, transform));
                }
            }

            // 2. 销毁ECS中不存在的GameObject（只处理指定类型的Entity）
            CleanupDestroyedEntities(world, entityType, currentEntityIds);

            // 3. 创建或更新GameObject
            foreach (var (entity, transform) in entitiesWithTransform)
            {
                GameObject gameObject = null;

                // 尝试获取已存在的GameObject
                if (getGameObject != null)
                {
                    gameObject = getGameObject(entity, transform);
                }
                else if (_entityToGameObject.TryGetValue(entity.Id, out gameObject))
                {
                    // GameObject已存在
                }
                else
                {
                    // 创建新的GameObject
                    gameObject = CreateEntityGameObject(entity, entityType, prefab);
                    if (gameObject != null)
                    {
                        _entityToGameObject[entity.Id] = gameObject;
                        _entityTypeMap[entity.Id] = entityType;
                    }

                    gameObject.transform.position = (Vector2)transform.position;
                }

                // 更新位置
                if (gameObject != null)
                {
                    UpdateGameObjectPosition(gameObject, transform.position);
                }
            }
        }

        /// <summary>
        /// 清理已销毁的Entity对应的GameObject
        /// </summary>
        private static void CleanupDestroyedEntities(World world, string entityType, HashSet<int> currentEntityIds)
        {
            var entitiesToRemove = new List<int>();

            foreach (var (entityId, gameObject) in _entityToGameObject)
            {
                // 跳过玩家Entity（由RegisterPlayer管理）
                if (_entityToPlayerId.ContainsKey(entityId))
                    continue;

                // 只处理指定类型的Entity
                if (!_entityTypeMap.TryGetValue(entityId, out var type) || type != entityType)
                    continue;

                // 如果Entity不在当前列表中，说明已被销毁
                if (!currentEntityIds.Contains(entityId))
                {
                    if (gameObject != null)
                    {
                        Object.Destroy(gameObject);
                    }

                    entitiesToRemove.Add(entityId);
                }
            }

            // 移除映射
            foreach (var entityId in entitiesToRemove)
            {
                _entityToGameObject.Remove(entityId);
                _entityTypeMap.Remove(entityId);
            }
        }

        /// <summary>
        /// 创建Entity对应的GameObject
        /// </summary>
        private static GameObject CreateEntityGameObject(Entity entity, string entityType, GameObject prefab)
        {
            GameObject gameObject = null;

            if (prefab != null)
            {
                // 使用Prefab实例化
                gameObject = Object.Instantiate(prefab);
            }
            else
            {
                // 创建默认GameObject（根据类型）
                gameObject = CreateDefaultGameObject(entityType);
            }

            if (gameObject != null)
            {
                gameObject.name = $"{entityType}_{entity.Id}";
            }

            return gameObject;
        }

        /// <summary>
        /// 创建默认GameObject（当没有Prefab时）
        /// </summary>
        private static GameObject CreateDefaultGameObject(string entityType)
        {
            GameObject gameObject = null;

            switch (entityType)
            {
                case "Bullet":
                    // 创建红色小球
                    gameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    var bulletRenderer = gameObject.GetComponent<Renderer>();
                    if (bulletRenderer != null)
                    {
                        bulletRenderer.material.color = Color.red;
                    }

                    break;

                case "Zombie":
                    // 创建绿色方块
                    gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    var zombieRenderer = gameObject.GetComponent<Renderer>();
                    if (zombieRenderer != null)
                    {
                        zombieRenderer.material.color = Color.green;
                    }

                    break;
            }

            // 移除碰撞器（物理由ECS处理）
            if (gameObject != null)
            {
                var collider = gameObject.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }
            }

            return gameObject;
        }

        /// <summary>
        /// 更新GameObject位置（统一的位置更新逻辑）
        /// </summary>
        private static void UpdateGameObjectPosition(GameObject gameObject, FixVector2 position)
        {
            if (gameObject == null)
                return;

            Vector3 targetPosition = new Vector3((float)position.x, (float)position.y, 0);

            if (ECSFrameSyncExample.Instance.isSmooth)
            {
                gameObject.transform.position = Vector3.Lerp(
                    gameObject.transform.position,
                    targetPosition,
                    ECSFrameSyncExample.Instance.smoothNum * Time.deltaTime
                );
            }
            else
            {
                gameObject.transform.position = targetPosition;
            }
        }

        /// <summary>
        /// 通过playerId获取Entity
        /// </summary>
        public static Entity? GetEntityByPlayerId(int playerId)
        {
            if (_playerIdToEntity.TryGetValue(playerId, out var entity))
            {
                return entity;
            }

            return null;
        }

        /// <summary>
        /// 通过Entity获取playerId
        /// </summary>
        public static int? GetPlayerIdByEntity(Entity entity)
        {
            if (_entityToPlayerId.TryGetValue(entity.Id, out var playerId))
            {
                return playerId;
            }

            return null;
        }

        /// <summary>
        /// 清空所有映射
        /// </summary>
        public static void Clear()
        {
            _entityToGameObject.Clear();
            _playerIdToEntity.Clear();
            _entityToPlayerId.Clear();
            _entityTypeMap.Clear();
        }
    }
}
