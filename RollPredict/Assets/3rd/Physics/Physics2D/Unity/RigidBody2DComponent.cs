using System.Collections.Generic;
using UnityEngine;
using Frame.FixMath;

namespace Frame.Physics2D
{
    /// <summary>
    /// Unity组件：2D刚体
    /// </summary>
    public class RigidBody2DComponent : MonoBehaviour
    {
        /// <summary>
        /// 是否是触发器
        /// </summary>
        public bool isTigger = false;

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


        public List<Physics2D.RigidBody2D> Stay => Body.Stay;
        public List<Physics2D.RigidBody2D> Enter => Body.Enter;
        public List<Physics2D.RigidBody2D> Exit => Body.Exit;

        /// <summary>
        /// 形状类型
        /// </summary>
        public enum ShapeType
        {
            Circle,
            Box
        }

        public ShapeType shapeType = ShapeType.Circle;

        /// <summary>
        /// 碰撞的位置偏移 
        /// </summary>
        public Vector2 posOffset = Vector2.zero;

        /// <summary>
        /// 圆形半径（当shapeType为Circle时使用）
        /// </summary>
        public float circleRadius = 0.5f;

        /// <summary>
        /// 矩形尺寸（当shapeType为Box时使用）
        /// </summary>
        public Vector2 boxSize = Vector2.one;

        /// <summary>
        /// 旋转角度（度，仅用于矩形，手动设置）
        /// </summary>
        [Range(-180f, 180f)] public float rotation = 0f;
        
        
        /// <summary>
        /// 物理体实例
        /// </summary>
        public RigidBody2D Body { get; private set; }

        public void Init(FixVector2 pos,PhysicsLayer layer)
        {
            // 创建碰撞形状
            CollisionShape2D shape;
            if (shapeType == ShapeType.Circle)
            {
                shape = new CircleShape2D((Fix64)circleRadius);
            }
            else
            {
                shape = new BoxShape2D((Fix64)boxSize.x, (Fix64)boxSize.y, (Fix64)rotation*Fix64.Deg2Rad);
            }

  
            Body = new RigidBody2D(
                new FixVector2((Fix64)pos.x + (Fix64)posOffset.x, (Fix64)pos.y + (Fix64)posOffset.y),
                (Fix64)mass,
                shape
            );
            Body.IsTrigger = isTigger;
            Body.UseGravity = useGravity;
            Body.Restitution = (Fix64)restitution;
            Body.Friction = (Fix64)friction;
            Body.LinearDamping = (Fix64)linearDamping;
            Body.IsStatic = isStatic;
            Body.gameObject = gameObject;
            Body.Layer = layer;
    
            // 添加到物理世界
            PhysicsWorld2DComponent.Instance.World.AddBody(Body);
        }

        
        private void Update()
        {
            // 同步物理位置和旋转到Unity Transform
            if (Body != null)
            {
                FixVector2 pos = Body.Position;
                transform.position = new Vector3((float)pos.x, (float)pos.y, transform.position.z) - (Vector3)posOffset;

                // 同步旋转（仅对矩形有效）
                if (shapeType == ShapeType.Box && Body.Shape is BoxShape2D q)
                {
                    float rotationDegrees = (float)q.Rotation;
                    transform.rotation = Quaternion.Euler(0, 0, rotationDegrees);
                }
            }
        }


        /// <summary>
        /// 在编辑器中可视化碰撞形状
        /// </summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = (mass > 0 && !isStatic) ? Color.green : Color.red;

            if (shapeType == ShapeType.Circle)
            {
                // 绘制圆形
                Vector3 center = transform.position + (Vector3)posOffset;
                float radius = circleRadius;

                // 绘制圆形轮廓
                int segments = 32;
                float angleStep = 360f / segments;
                Vector3 prevPoint = center + new Vector3(radius, 0, 0);

                for (int i = 1; i <= segments; i++)
                {
                    float angle = i * angleStep * Mathf.Deg2Rad;
                    Vector3 point = center + new Vector3(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius,
                        0
                    );
                    Gizmos.DrawLine(prevPoint, point);
                    prevPoint = point;
                }

              
            }
            else
            {
                // 绘制矩形（支持旋转）
                Vector3 center = transform.position + (Vector3)posOffset;
                Vector3 size = new Vector3(boxSize.x, boxSize.y, 0);
                Vector3 halfSize = size * 0.5f;

                // 计算旋转后的四个角点
                float rotationRad = rotation *Mathf.Deg2Rad; ;
                float cos = Mathf.Cos(rotationRad);
                float sin = Mathf.Sin(rotationRad);

                Vector3[] corners = new Vector3[]
                {
                    new Vector3(-halfSize.x, -halfSize.y, 0), // 左下
                    new Vector3(halfSize.x, -halfSize.y, 0), // 右下
                    new Vector3(halfSize.x, halfSize.y, 0), // 右上
                    new Vector3(-halfSize.x, halfSize.y, 0) // 左上
                };

                // 旋转并转换到世界坐标
                for (int i = 0; i < 4; i++)
                {
                    float x = corners[i].x;
                    float y = corners[i].y;
                    corners[i] = center + new Vector3(
                        x * cos - y * sin,
                        x * sin + y * cos,
                        0
                    );
                }

                // 绘制矩形边框
                Gizmos.DrawLine(corners[0], corners[1]);
                Gizmos.DrawLine(corners[1], corners[2]);
                Gizmos.DrawLine(corners[2], corners[3]);
                Gizmos.DrawLine(corners[3], corners[0]);


            }
            Gizmos.color = Color.yellow;
            if (Application.isPlaying&& Body!=null)
            {
                var bound = Body.Shape.GetBounds((FixVector2)(Vector2)transform.position);

                Gizmos.DrawLine(new Vector2((float)bound.X, (float)bound.Y),
                    new Vector2((float)(bound.X + bound.Width), (float)bound.Y));
                Gizmos.DrawLine(new Vector2((float)(bound.X + bound.Width), (float)bound.Y),
                    new Vector2((float)(bound.X + bound.Width), (float)(bound.Y + bound.Height)));
                Gizmos.DrawLine(new Vector2((float)(bound.X + bound.Width), (float)(bound.Y + bound.Height)),
                    new Vector2((float)(bound.X), (float)(bound.Y + bound.Height)));
                Gizmos.DrawLine(new Vector2((float)(bound.X), (float)(bound.Y + bound.Height)),
                    new Vector2((float)bound.X, (float)bound.Y));
            }
        }
    }
}