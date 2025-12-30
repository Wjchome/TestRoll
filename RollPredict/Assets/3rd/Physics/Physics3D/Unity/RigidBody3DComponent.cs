using System;
using System.Collections.Generic;
using UnityEngine;
using Frame.FixMath;
using PhysicsLayer = Frame.Physics2D.PhysicsLayer; // 复用2D的Layer系统

namespace Frame.Physics3D
{
    /// <summary>
    /// Unity组件：3D刚体
    /// </summary>
    public class RigidBody3DComponent : MonoBehaviour
    {
        /// <summary>
        /// 是否是触发器
        /// </summary>
        public bool isTrigger = false;

        /// <summary>
        /// 是否为静态物品
        /// </summary>
        public bool isStatic = false;

        /// <summary>
        /// 质量(负数或者0会当作静态物品)
        /// </summary>
        public float mass = 1f;

        /// <summary>
        /// 是否受重力影响
        /// </summary>
        public bool useGravity = true;

        /// <summary>
        /// 弹性系数（0-1）
        /// </summary>
        [Range(0f, 1f)] public float restitution = 0f;

        /// <summary>
        /// 摩擦系数（0-1）
        /// </summary>
        [Range(0f, 1f)] public float friction = 0.5f;

        /// <summary>
        /// 线性阻尼（0-1，用于在空地上减速）
        /// 值越大，减速越快。0表示无阻尼，1表示完全停止
        /// 例如：0.1表示每秒减少10%的速度
        /// </summary>
        [Range(0f, 1f)] public float linearDamping = 0f;

        public List<Physics3D.RigidBody3D> Stay => Body.Stay;
        public List<Physics3D.RigidBody3D> Enter => Body.Enter;
        public List<Physics3D.RigidBody3D> Exit => Body.Exit;

        /// <summary>
        /// 形状类型
        /// </summary>
        public enum ShapeType
        {
            Sphere,
            Box
        }

        public ShapeType shapeType = ShapeType.Sphere;

        /// <summary>
        /// 碰撞的位置偏移 
        /// </summary>
        public Vector3 posOffset = Vector3.zero;

        /// <summary>
        /// 球体半径（当shapeType为Sphere时使用）
        /// </summary>
        public float sphereRadius = 0.5f;

        /// <summary>
        /// 长方体尺寸（当shapeType为Box时使用）
        /// </summary>
        public Vector3 boxSize = Vector3.one;
        
        /// <summary>
        /// 物理体实例
        /// </summary>
        public RigidBody3D Body { get; private set; }

        /// <summary>
        /// 初始化物理体
        /// </summary>
        /// <param name="pos">初始位置（定点数）</param>
        /// <param name="layer">物理层</param>
        public void Init(FixVector3 pos, PhysicsLayer layer)
        {
            // 创建碰撞形状
            CollisionShape3D shape;
            if (shapeType == ShapeType.Sphere)
            {
                shape = new SphereShape3D((Fix64)sphereRadius);
            }
            else
            {
                shape = new BoxShape3D((Fix64)boxSize.x, (Fix64)boxSize.y, (Fix64)boxSize.z);
            }

            Body = new RigidBody3D(
                new FixVector3(
                    pos.x + (Fix64)posOffset.x,
                    pos.y + (Fix64)posOffset.y,
                    pos.z + (Fix64)posOffset.z
                ),
                (Fix64)mass,
                shape
            );
            Body.IsTrigger = isTrigger;
            Body.UseGravity = useGravity;
            Body.Restitution = (Fix64)restitution;
            Body.Friction = (Fix64)friction;
            Body.LinearDamping = (Fix64)linearDamping;
            Body.IsStatic = isStatic;
            Body.gameObject = gameObject;
            Body.Layer = layer;
    
            // 添加到物理世界
            PhysicsWorld3DComponent.Instance.World.AddBody(Body);
        }
        

        private void Update()
        {
            
            // 同步物理位置到Unity Transform
            if (Body != null)
            {
                FixVector3 pos = Body.Position;
                transform.position = new Vector3(
                    (float)pos.x,
                    (float)pos.y,
                    (float)pos.z
                ) - posOffset;
            }
        }

        /// <summary>
        /// 在编辑器中可视化碰撞形状
        /// </summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = (mass > 0 && !isStatic) ? Color.green : Color.red;

            Vector3 center = transform.position + posOffset;

            if (shapeType == ShapeType.Sphere)
            {
                // 绘制球体
                float radius = sphereRadius;
                
                // 绘制球体的三个圆环（XY、XZ、YZ平面）
                int segments = 32;
                float angleStep = 360f / segments;

                // XY平面圆环
                Vector3 prevPointXY = center + new Vector3(radius, 0, 0);
                for (int i = 1; i <= segments; i++)
                {
                    float angle = i * angleStep * Mathf.Deg2Rad;
                    Vector3 point = center + new Vector3(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius,
                        0
                    );
                    Gizmos.DrawLine(prevPointXY, point);
                    prevPointXY = point;
                }

                // XZ平面圆环
                Vector3 prevPointXZ = center + new Vector3(radius, 0, 0);
                for (int i = 1; i <= segments; i++)
                {
                    float angle = i * angleStep * Mathf.Deg2Rad;
                    Vector3 point = center + new Vector3(
                        Mathf.Cos(angle) * radius,
                        0,
                        Mathf.Sin(angle) * radius
                    );
                    Gizmos.DrawLine(prevPointXZ, point);
                    prevPointXZ = point;
                }

                // YZ平面圆环
                Vector3 prevPointYZ = center + new Vector3(0, radius, 0);
                for (int i = 1; i <= segments; i++)
                {
                    float angle = i * angleStep * Mathf.Deg2Rad;
                    Vector3 point = center + new Vector3(
                        0,
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius
                    );
                    Gizmos.DrawLine(prevPointYZ, point);
                    prevPointYZ = point;
                }
            }
            else
            {
                // 绘制长方体（轴对齐）
                Vector3 size = boxSize;
                Vector3 halfSize = size * 0.5f;

                // 计算八个角点
                Vector3[] corners = new Vector3[]
                {
                    center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), // 左下后
                    center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z),  // 右下后
                    center + new Vector3(halfSize.x, halfSize.y, -halfSize.z),   // 右上后
                    center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z),  // 左上后
                    center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z),   // 左下前
                    center + new Vector3(halfSize.x, -halfSize.y, halfSize.z),    // 右下前
                    center + new Vector3(halfSize.x, halfSize.y, halfSize.z),     // 右上前
                    center + new Vector3(-halfSize.x, halfSize.y, halfSize.z)     // 左上前
                };

                // 绘制12条边
                // 后面四条边
                Gizmos.DrawLine(corners[0], corners[1]);
                Gizmos.DrawLine(corners[1], corners[2]);
                Gizmos.DrawLine(corners[2], corners[3]);
                Gizmos.DrawLine(corners[3], corners[0]);

                // 前面四条边
                Gizmos.DrawLine(corners[4], corners[5]);
                Gizmos.DrawLine(corners[5], corners[6]);
                Gizmos.DrawLine(corners[6], corners[7]);
                Gizmos.DrawLine(corners[7], corners[4]);

                // 连接前后面的四条边
                Gizmos.DrawLine(corners[0], corners[4]);
                Gizmos.DrawLine(corners[1], corners[5]);
                Gizmos.DrawLine(corners[2], corners[6]);
                Gizmos.DrawLine(corners[3], corners[7]);
            }

            // 绘制运行时AABB边界（黄色）
            Gizmos.color = Color.yellow;
            if (Application.isPlaying && Body != null)
            {
                var bounds = Body.Shape.GetBounds(Body.Position);
                Vector3 min = new Vector3((float)bounds.Min.x, (float)bounds.Min.y, (float)bounds.Min.z);
                Vector3 max = new Vector3((float)bounds.Max.x, (float)bounds.Max.y, (float)bounds.Max.z);

                // 绘制AABB的12条边
                Vector3[] aabbCorners = new Vector3[]
                {
                    new Vector3(min.x, min.y, min.z), // 左下后
                    new Vector3(max.x, min.y, min.z), // 右下后
                    new Vector3(max.x, max.y, min.z), // 右上后
                    new Vector3(min.x, max.y, min.z), // 左上后
                    new Vector3(min.x, min.y, max.z), // 左下前
                    new Vector3(max.x, min.y, max.z), // 右下前
                    new Vector3(max.x, max.y, max.z), // 右上前
                    new Vector3(min.x, max.y, max.z)  // 左上前
                };

                // 后面四条边
                Gizmos.DrawLine(aabbCorners[0], aabbCorners[1]);
                Gizmos.DrawLine(aabbCorners[1], aabbCorners[2]);
                Gizmos.DrawLine(aabbCorners[2], aabbCorners[3]);
                Gizmos.DrawLine(aabbCorners[3], aabbCorners[0]);

                // 前面四条边
                Gizmos.DrawLine(aabbCorners[4], aabbCorners[5]);
                Gizmos.DrawLine(aabbCorners[5], aabbCorners[6]);
                Gizmos.DrawLine(aabbCorners[6], aabbCorners[7]);
                Gizmos.DrawLine(aabbCorners[7], aabbCorners[4]);

                // 连接前后面的四条边
                Gizmos.DrawLine(aabbCorners[0], aabbCorners[4]);
                Gizmos.DrawLine(aabbCorners[1], aabbCorners[5]);
                Gizmos.DrawLine(aabbCorners[2], aabbCorners[6]);
                Gizmos.DrawLine(aabbCorners[3], aabbCorners[7]);
            }
        }
    }
}

