using System;
using System.Collections.Generic;
using Frame.FixMath;
using UnityEngine;
using PhysicsLayer = Frame.Physics2D.PhysicsLayer; // 复用2D的Layer系统

namespace Frame.Physics3D
{
    /// <summary>
    /// 3D刚体（不考虑旋转，只处理平移运动）
    /// 基于定点数，用于帧同步
    /// </summary>
    public class RigidBody3D : IComparable<RigidBody3D>, IEquatable<RigidBody3D>
    {
        /// <summary>
        /// 是否是触发器
        /// </summary>
        public bool IsTrigger { get; set; }

        /// <summary>
        /// 位置（世界坐标）
        /// </summary>
        public FixVector3 Position { get; set; }

        /// <summary>
        /// 速度（单位：单位/秒）
        /// </summary>
        public FixVector3 Velocity { get; set; }

        /// <summary>
        /// 质量（单位：千克）
        /// 质量为0或负数表示静态物体（不受力影响）
        /// </summary>
        public Fix64 Mass { get; set; }

        /// <summary>
        /// 是否受重力影响
        /// </summary>
        public bool UseGravity { get; set; } = true;

        /// <summary>
        /// 是否为动态物体（false表示静态/运动学物体）
        /// </summary>
        public bool IsDynamic => !IsStatic && Mass > Fix64.Zero;

        /// <summary>
        /// 碰撞形状（长方体或球体）
        /// </summary>
        public CollisionShape3D Shape { get; set; }

        /// <summary>
        /// 所属的物理世界
        /// </summary>
        public PhysicsWorld3D World { get; internal set; }

        public bool IsStatic { get; set; } = false;

        /// <summary>
        /// 判断层，几乎和Unity的Tag一个作用，但是可以更加更加细节
        /// </summary>
        public PhysicsLayer Layer { get; set; }

        /// <summary>
        /// 弹性系数（0-1，0表示完全非弹性，1表示完全弹性）
        /// </summary>
        public Fix64 Restitution { get; set; } = Fix64.Zero;

        /// <summary>
        /// 摩擦系数（0-1）
        /// </summary>
        public Fix64 Friction { get; set; } = (Fix64)0.5m;

        /// <summary>
        /// 线性阻尼（0-1，用于在空地上减速）
        /// 值越大，减速越快。0表示无阻尼，1表示完全停止
        /// 例如：0.1表示每秒减少10%的速度
        /// </summary>
        public Fix64 LinearDamping { get; set; } = Fix64.Zero;

        /// <summary>
        /// 力累加器（每帧累积所有力，在Update中统一处理）
        /// </summary>
        internal FixVector3 ForceAccumulator { get; set; } = FixVector3.Zero;

        public GameObject gameObject;

        public int id;
        
        /// <summary>
        /// 上一帧的AABB（用于快速比较，避免不必要的更新）
        /// </summary>
        internal FixBounds PreviousBounds { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="position">初始位置</param>
        /// <param name="mass">质量（0或负数表示静态物体）</param>
        /// <param name="shape">碰撞形状</param>
        public RigidBody3D(FixVector3 position, Fix64 mass, CollisionShape3D shape)
        {
            Position = position;
            Mass = mass;
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            Velocity = FixVector3.Zero;
        }

        /// <summary>
        /// 应用力（累积到力累加器中，不会立即改变速度）
        /// 力会在物理世界的Update循环中统一处理
        /// </summary>
        /// <param name="force">力向量</param>
        public void ApplyForce(FixVector3 force)
        {
            if (!IsDynamic) return;
            // 累积力，不直接修改速度
            ForceAccumulator += force;
        }

        /// <summary>
        /// 清除力累加器（每帧开始时调用）
        /// </summary>
        internal void ClearForces()
        {
            ForceAccumulator = FixVector3.Zero;
        }

        /// <summary>
        /// 应用冲量（直接改变速度）
        /// </summary>
        /// <param name="impulse">冲量向量</param>
        public void ApplyImpulse(FixVector3 impulse)
        {
            if (!IsDynamic) return;

            // J = mv => Δv = J/m
            Velocity += impulse / Mass;
        }

        #region 接口实现

        /// <summary>
        /// IComparable接口实现，使用id进行比较，确保确定性排序
        /// </summary>
        public int CompareTo(RigidBody3D other)
        {
            return id.CompareTo(other.id);
        }

        /// <summary>
        /// IEquatable接口实现，使用id进行比较，确保确定性相等判断
        /// </summary>
        public bool Equals(RigidBody3D other)
        {
            return id == other.id;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RigidBody3D);
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

        #endregion

        #region 供外部使用

        public List<RigidBody3D> LastRigidBody3D = new List<RigidBody3D>();
        public List<RigidBody3D> CurrentRigidBody3D = new List<RigidBody3D>();

        public List<RigidBody3D> Enter = new List<RigidBody3D>();
        public List<RigidBody3D> Stay = new List<RigidBody3D>();
        public List<RigidBody3D> Exit = new List<RigidBody3D>();

        #endregion

        public override string ToString()
        {
            return $"RigidBody3D: {id} name:{gameObject}";
        }
    }
}

