using Frame.FixMath;
using UnityEngine;

namespace Frame.ECS
{
    public static class AddForceHelper
    {
        public static void ApplyForce(World world, Entity entity, FixVector2 force)
        {
            if (!world.TryGetComponent<VelocityComponent>(entity,
                    out var velocityComponent))
            {
                Debug.LogError("缺失组件");
                return;
            }
            if (!world.TryGetComponent<PhysicsBodyComponent>(entity,
                    out var physicsBodyComponent))
            {
                Debug.LogError("缺失组件");
                return;
            }

            velocityComponent.velocity += force / physicsBodyComponent.mass;
            world.AddComponent(entity, velocityComponent);
        }
    }
}