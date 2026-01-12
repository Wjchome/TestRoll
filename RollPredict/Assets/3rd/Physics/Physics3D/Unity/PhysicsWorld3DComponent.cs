using Frame.Core;
using UnityEngine;
using Frame.FixMath;
using PhysicsLayer = Frame.Physics2D.PhysicsLayer; // 复用2D的Layer系统

namespace Frame.Physics3D
{
    /// <summary>
    /// Unity组件：物理世界管理器（3D）
    /// </summary>
    public class PhysicsWorld3DComponent : SingletonMono<PhysicsWorld3DComponent>
    {
        /// <summary>
        /// 物理世界实例
        /// </summary>
        public PhysicsWorld3D World { get; private set; }

        /// <summary>
        /// 重力（Unity单位）
        /// </summary>
        public Vector3 gravity = new Vector3(0, -9.81f, 0);
        
        /// <summary>
        /// 迭代次数（用于碰撞分离，提高稳定性）
        /// </summary>
        public int iterations = 8;

        /// <summary>
        /// 子步迭代次数（将一个时间步分成多个子步，提高物理模拟稳定性）
        /// 子步迭代可以有效处理高速移动物体，避免穿透等问题
        /// </summary>
        [Tooltip("子步迭代次数")]
        public int subSteps = 2;

        //"每个节点最大存储物体数（超过则分裂）
        public int maxObjectsPerNode;
        
        //"最大递归深度（防止无限分裂）
        public int maxDepth;
        private void Awake()
        {
            // 创建物理世界
            World = new PhysicsWorld3D();
            World.Gravity = new FixVector3((Fix64)gravity.x, (Fix64)gravity.y, (Fix64)gravity.z);
            World.Iterations = iterations;
            World.SubSteps = subSteps;
            World.bvh.MaxObjectsPerNode = maxObjectsPerNode;
            World.bvh.MaxDepth = maxDepth;

            // 可以在这里设置Layer碰撞忽略
            // World.IgnoreLayerCollision(PhysicsLayer.GetLayer(0), PhysicsLayer.GetLayer(1));
        }

        /// <summary>
        /// 添加刚体到物理世界
        /// </summary>
        /// <param name="rigidBody">刚体组件</param>
        /// <param name="pos">初始位置（定点数）</param>
        /// <param name="layer">物理层</param>
        public void AddRigidBody(RigidBody3DComponent rigidBody, FixVector3 pos, PhysicsLayer layer)
        {
            rigidBody.Init(pos, layer);
        }

        /// <summary>
        /// 更新物理世界（每帧调用）
        /// </summary>
        public void UpdateFrame()
        {
            // 更新物理世界
            if (World != null)
            {
                World.Update();
            }
        }

        private void OnDestroy()
        {
            if (World != null)
            {
                World.Clear();
            }
        }
        
        private void OnDrawGizmos()
        {
            if (World == null || World.bvh == null) return;

            World.bvh.DrawGizmos();
        }
    }
}

