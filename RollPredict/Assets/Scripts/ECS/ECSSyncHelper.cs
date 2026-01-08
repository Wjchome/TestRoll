using System.Collections.Generic;
using Frame.ECS;
using Frame.ECS.Components;
using Frame.FixMath;
using UnityEngine;

namespace Frame.ECS
{
    /// <summary>
    /// ECS同步辅助类：在ECS World和Unity对象之间同步状态
    /// 用于视图层显示
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
        /// 注册玩家到ECS系统
        /// </summary>
        public static Entity RegisterPlayer(World world, int playerId, GameObject gameObject, FixVector2 initialPosition, int initialHp)
        {
            // 创建Entity
            Entity entity = world.CreateEntity();

            // 添加PlayerComponent
            var playerComponent = new PlayerComponent(playerId, initialPosition, initialHp);
            world.AddComponent(entity, playerComponent);

            // 建立映射
            _entityToGameObject[entity.Id] = gameObject;
            _playerIdToEntity[playerId] = entity;
            _entityToPlayerId[entity.Id] = playerId;

            return entity;
        }

        /// <summary>
        /// 从ECS World同步状态到Unity对象（用于视图层显示）
        /// World -> Unity
        /// </summary>
        public static void SyncFromWorldToUnity(World world)
        {
            // 同步玩家状态
            foreach (var entity in world.GetEntitiesWithComponent<PlayerComponent>())
            {
                if (!world.TryGetComponent<PlayerComponent>(entity, out var playerComponent))
                    continue;

                // 查找对应的Unity对象
                if (!_entityToGameObject.TryGetValue(entity.Id, out var gameObject))
                    continue;

                // 更新Unity对象的位置
                gameObject.transform.position = new Vector3(
                    (float)playerComponent.position.x,
                    (float)playerComponent.position.y,
                    0
                );
            }

            // 同步子弹状态
            SyncBullets(world);
        }

        /// <summary>
        /// 同步子弹：创建、更新、销毁
        /// </summary>
        private static void SyncBullets(World world)
        {
            // 获取所有ECS中存在的子弹Entity
            var activeBulletEntities = new HashSet<int>();
            foreach (var entity in world.GetEntitiesWithComponent<BulletComponent>())
            {
                activeBulletEntities.Add(entity.Id);
            }

            if (activeBulletEntities.Count > 0)
            {
                int a = 0;
            }
            // 1. 销毁ECS中不存在的子弹GameObject
            var entitiesToRemove = new List<int>();
            foreach (var kvp in _entityToGameObject)
            {
                var entityId = kvp.Key;
                var gameObject = kvp.Value;

                // 如果不是玩家（玩家在 _entityToPlayerId 中）
                if (!_entityToPlayerId.ContainsKey(entityId))
                {
                    // 如果ECS中不存在这个子弹Entity，销毁GameObject
                    if (!activeBulletEntities.Contains(entityId))
                    {
                        if (gameObject != null)
                        {
                            Object.Destroy(gameObject);
                        }
                        entitiesToRemove.Add(entityId);
                    }
                }
            }

            // 移除映射
            foreach (var entityId in entitiesToRemove)
            {
                _entityToGameObject.Remove(entityId);
            }

            // 2. 更新或创建子弹GameObject
            foreach (var entity in world.GetEntitiesWithComponent<BulletComponent>())
            {
                if (!world.TryGetComponent<BulletComponent>(entity, out var bulletComponent))
                    continue;

                // 如果GameObject不存在，创建它
                if (!_entityToGameObject.TryGetValue(entity.Id, out var bulletGameObject))
                {
                  
                        bulletGameObject = GameObject.Instantiate(ECSFrameSyncExample.Instance.bulletPrefab);
                        bulletGameObject.name = $"Bulle_{entity.Id}";
                        _entityToGameObject[entity.Id] = bulletGameObject;
               
                }

                // 更新子弹位置
                if (bulletGameObject != null)
                {
                    bulletGameObject.transform.position = new Vector3(
                        (float)bulletComponent.position.x,
                        (float)bulletComponent.position.y,
                        0
                    );
                }
            }
        }

        /// <summary>
        /// 从Unity对象同步状态到ECS World（用于保存状态）
        /// Unity -> World
        /// </summary>
        public static void SyncFromUnityToWorld(World world)
        {
            // 同步玩家状态
            foreach (var kvp in _playerIdToEntity)
            {
                var playerId = kvp.Key;
                var entity = kvp.Value;

                if (!_entityToGameObject.TryGetValue(entity.Id, out var gameObject))
                    continue;


                // 从Unity对象获取状态
                FixVector2 position = new FixVector2(
                    (Fix64)gameObject.transform.position.x,
                    (Fix64)gameObject.transform.position.y
                );

                // 更新PlayerComponent
                var playerComponent = new PlayerComponent(playerId, position, 100);
                world.AddComponent(entity, playerComponent);
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
        }
    }
}

