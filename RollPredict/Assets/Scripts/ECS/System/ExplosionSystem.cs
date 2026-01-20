using System.Collections.Generic;
using Frame.FixMath;
using Proto;
using UnityEngine;

namespace Frame.ECS
{
    /// <summary>
    /// 爆炸系统：处理爆炸的伤害和生命周期
    /// 
    /// 职责：
    /// - 在爆炸创建的第一帧造成范围伤害
    /// - 更新爆炸的生命周期
    /// - 销毁过期的爆炸Entity
    /// 
    /// 伤害规则：
    /// - 对所有有HPComponent的Entity造成伤害
    /// - 可以连锁引爆油桶
    /// - 可以伤害玩家、僵尸、油桶等
    /// </summary>
    public class ExplosionSystem : ISystem
    {
        public const int lifetimeFrames = 2;
        public void Execute(World world, List<FrameData> inputs)
        {
            var physicsSystem = ECSStateMachine.GetSystem<PhysicsSystem>();
            var explosionsToRemove = new List<Entity>();
            
            foreach (var (entity, explosion, transform) in 
                     world.GetEntitiesWithComponents<ExplosionComponent, Transform2DComponent>())
            {
                var updatedExplosion = explosion;
                
                
                // 2. 更新生命周期
                updatedExplosion.currentFrame++;
                // 3. 检查是否过期
                if (updatedExplosion.currentFrame >= lifetimeFrames)
                {
                    DealExplosionDamage(world, physicsSystem, updatedExplosion);
                    
                    explosionsToRemove.Add(entity);
                    
                }
                else
                {
                    // 更新组件
                    world.AddComponent(entity, updatedExplosion);
                }
            }
            
            // 4. 销毁过期的爆炸Entity
            foreach (var entity in explosionsToRemove)
            {

                world.DestroyEntity(entity);
            }
        }

        /// <summary>
        /// 造成爆炸伤害
        /// </summary>
        private void DealExplosionDamage(World world, PhysicsSystem physicsSystem, ExplosionComponent explosion)
        {
            // 使用物理系统的QueryCircleRegion查询范围内的所有Entity
            var entitiesInRange = physicsSystem.QueryCircleRegion(
                world,
                explosion.position,
                explosion.radius,
                -1  // 查询所有Layer
            );
            
            Debug.Log($"[ExplosionSystem] 爆炸检测到 {entitiesInRange.Count} 个Entity");
            
            foreach (var targetEntity in entitiesInRange)
            {

                // 检查目标是否有HPComponent
                if (world.TryGetComponent<HPComponent>(targetEntity, out var hp))
                {
                    HPDamageHelper.ApplyDamage(world,targetEntity,explosion.damage);
                }
                
                
            }
        }
    }
}



