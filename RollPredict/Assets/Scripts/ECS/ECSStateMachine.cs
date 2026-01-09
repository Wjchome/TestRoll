using System.Collections.Generic;
using System.Linq;
using Frame.ECS;
using Frame.ECS.Components;
using Frame.FixMath;
using Proto;

namespace Frame.ECS
{
    /// <summary>
    /// ECS版本的状态机
    /// 实现 State(n+1) = StateMachine(State(n), Input(n))
    /// 
    /// 设计：
    /// - 使用ECS World存储所有游戏状态
    /// - 输入处理：移动玩家、发射子弹
    /// - 系统更新：更新子弹位置等
    /// </summary>
    public static class ECSStateMachine
    {
        /// <summary>
        /// 玩家移动速度（固定点）
        /// </summary>
        public static Fix64 PlayerSpeed = (Fix64)0.1f;

        /// <summary>
        /// 子弹速度（固定点）
        /// </summary>
        public static Fix64 BulletSpeed = (Fix64)0.2f;

        /// <summary>
        /// 状态机核心函数：根据当前状态和输入计算下一帧状态
        /// State(n+1) = StateMachine(State(n), Input(n+1))
        /// </summary>
        /// <param name="world">当前帧的World状态 State(n)</param>
        /// <param name="inputs">当前帧所有玩家的输入 Input(n)</param>
        /// <param name="fireInputs">当前帧所有玩家的发射输入</param>
        /// <returns>下一帧的World状态 State(n+1)</returns>
        public static World Execute(World world, List<FrameData> inputs)
        {
            // 1. 处理玩家输入：移动
            ProcessPlayerMovement(world, inputs);

            // 2. 处理玩家输入：发射子弹
            ProcessPlayerFire(world, inputs);

            // 3. 更新子弹位置
            UpdateBullets(world);

            return world;
        }

        /// <summary>
        /// 处理玩家移动输入
        /// </summary>
        private static void ProcessPlayerMovement(World world, List<FrameData> inputs)
        {
            foreach (var frameData in inputs)
            {
               var (playerId, inputDirection)  = (frameData.PlayerId,frameData.Direction);
                // 跳过无输入
                if (inputDirection == InputDirection.DirectionNone)
                    continue;

                // 查找玩家的Entity（通过PlayerComponent的playerId）
                Entity? playerEntity = FindPlayerEntity(world, playerId);
                if (!playerEntity.HasValue)
                    continue;

                // 获取PlayerComponent
                if (!world.TryGetComponent<PlayerComponent>(playerEntity.Value, out var playerComponent))
                    continue;

                // 将输入方向转换为移动向量
                FixVector2 movementDirection = GetMovementDirection(inputDirection);

                // 更新玩家位置
                FixVector2 newPosition = playerComponent.position + movementDirection * PlayerSpeed;

                // 更新PlayerComponent
                var updatedComponent = new PlayerComponent(
                    playerComponent.playerId,
                    newPosition,
                    playerComponent.hp
                );
                world.AddComponent(playerEntity.Value, updatedComponent);
            }
        }

        /// <summary>
        /// 处理玩家发射子弹输入
        /// </summary>
        private static void ProcessPlayerFire(World world, List<FrameData> inputs)
        {
            foreach (var frameData in inputs)
            {
                // 检查是否发射子弹
                if (!frameData.IsFire)
                    continue;

                var playerId = frameData.PlayerId;

                // 查找玩家的Entity
                Entity? playerEntity = FindPlayerEntity(world, playerId);
                if (!playerEntity.HasValue)
                    continue;

                // 获取PlayerComponent
                if (!world.TryGetComponent<PlayerComponent>(playerEntity.Value, out var playerComponent))
                    continue;

                // 计算子弹方向（从玩家位置指向目标位置）
                FixVector2 bulletPosition = playerComponent.position;
                FixVector2 targetPosition = new FixVector2(Fix64.FromRaw(frameData.FireX),Fix64.FromRaw(frameData.FireY));
                
                // 计算方向向量并归一化
                FixVector2 direction = targetPosition - bulletPosition;
                Fix64 distance = Fix64.Sqrt(direction.x * direction.x + direction.y * direction.y);
                
                if (distance > Fix64.Zero)
                {
                    direction.Normalize();
                    FixVector2 bulletVelocity = direction*BulletSpeed ;

                    // 创建子弹Entity
                    // 注意：子弹不再需要单独的bulletId，Entity.Id本身就是唯一标识
                    Entity bulletEntity = world.CreateEntity();
                    var bulletComponent = new BulletComponent(
                        bulletPosition,
                        bulletVelocity,
                        playerEntity.Value.Id
                    );
                    world.AddComponent(bulletEntity, bulletComponent);
                }
            }
        }

        /// <summary>
        /// 更新子弹位置
        /// </summary>
        private static void UpdateBullets(World world)
        {
            var bulletsToUpdate = new List<Entity>();
            
            // 收集所有子弹Entity
            foreach (var entity in world.GetEntitiesWithComponent<BulletComponent>())
            {
                bulletsToUpdate.Add(entity);
            }

            // 更新每个子弹
            foreach (var bulletEntity in bulletsToUpdate)
            {
                if (!world.TryGetComponent<BulletComponent>(bulletEntity, out var bulletComponent))
                    continue;

                // 更新子弹位置
                FixVector2 newPosition = bulletComponent.position + bulletComponent.velocity;

                // 检查子弹是否超出边界（简单边界检查，可以扩展）
                // 这里假设世界边界是 -10 到 10
                if (newPosition.x < (Fix64)(-10) || newPosition.x > (Fix64)10 ||
                    newPosition.y < (Fix64)(-10) || newPosition.y > (Fix64)10)
                {
                    // 子弹超出边界，销毁
                    world.DestroyEntity(bulletEntity);
                    continue;
                }

                // 更新BulletComponent
                var updatedComponent = new BulletComponent(
                    newPosition,
                    bulletComponent.velocity,
                    bulletComponent.ownerEntityId
                );
                world.AddComponent(bulletEntity, updatedComponent);
            }
        }

        /// <summary>
        /// 通过playerId查找玩家的Entity
        /// </summary>
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

        /// <summary>
        /// 将输入方向转换为移动向量（FixVector2）
        /// 支持8个方向，斜向移动需要归一化
        /// </summary>
        private static FixVector2 GetMovementDirection(InputDirection direction)
        {
            // 斜向移动的归一化系数（sqrt(2)/2 ≈ 0.707）
            Fix64 diagonalFactor = (Fix64)0.7071067811865476m; // sqrt(2)/2

            switch (direction)
            {
                case InputDirection.DirectionUp:
                    return FixVector2.Up;

                case InputDirection.DirectionDown:
                    return FixVector2.Down;

                case InputDirection.DirectionLeft:
                    return FixVector2.Left;

                case InputDirection.DirectionRight:
                    return FixVector2.Right;

                case InputDirection.DirectionUpLeft:
                    return (FixVector2.Up + FixVector2.Left) * diagonalFactor;

                case InputDirection.DirectionUpRight:
                    return (FixVector2.Up + FixVector2.Right) * diagonalFactor;

                case InputDirection.DirectionDownLeft:
                    return (FixVector2.Down + FixVector2.Left) * diagonalFactor;

                case InputDirection.DirectionDownRight:
                    return (FixVector2.Down + FixVector2.Right) * diagonalFactor;

                case InputDirection.DirectionNone:
                default:
                    return FixVector2.Zero;
            }
        }
    }
}

