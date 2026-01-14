using System;
using System.Collections.Generic;
using Frame.FixMath;

namespace Frame.ECS
{
    /// <summary>
    /// 僵尸状态枚举
    /// </summary>
    public enum ZombieState : byte
    {
        Chase ,          // 追逐（寻路移动）
        AttackWindup ,   // 攻击前摇
        Attack ,         // 攻击中（伤害判定帧）
        AttackCooldown , // 攻击后摇
    }

    /// <summary>
    /// 僵尸AI组件：存储僵尸的AI状态和路径信息
    /// </summary>
    [Serializable]
    public struct ZombieAIComponent : IComponent
    {
        /// <summary>
        /// 目标位置（世界坐标）
        /// </summary>
        public FixVector2 targetPosition;
        
        /// <summary>
        /// 当前路径（世界坐标列表）
        /// </summary>
        public List<FixVector2> currentPath;
        
        /// <summary>
        /// 当前路径索引
        /// </summary>
        public int currentPathIndex;
        
        /// <summary>
        /// 移动速度
        /// </summary>
        public Fix64 moveSpeed;
        
        /// <summary>
        /// 寻路冷却时间（避免每帧都寻路）
        /// </summary>
        public int pathfindingCooldown;
        
        /// <summary>
        /// 当前状态
        /// </summary>
        public ZombieState state;
        
        /// <summary>
        /// 攻击方向（进入攻击状态时保存，用于伤害判定）
        /// </summary>
        public FixVector2 attackDirection;
        
        /// <summary>
        /// 攻击前摇计时器（帧数）
        /// </summary>
        public int attackWindupTimer;
        
        /// <summary>
        /// 攻击后摇计时器（帧数）
        /// </summary>
        public int attackCooldownTimer;
        
        /// <summary>
        /// 攻击检测冷却（每N帧检测一次攻击范围）
        /// </summary>
        public int attackDetectionCooldown;
        
        /// <summary>
        /// 攻击范围（圆形半径）
        /// </summary>
        public Fix64 attackRange;
        
        
        /// <summary>
        /// 攻击伤害
        /// </summary>
        public int attackDamage;
        
        /// <summary>
        /// 攻击前摇时间（帧数）
        /// </summary>
        public int attackWindupFrames;
        
        /// <summary>
        /// 攻击后摇时间（帧数）
        /// </summary>
        public int attackCooldownFrames;
        
        /// <summary>
        /// 攻击伤害判定距离（前方扇形/矩形的距离）
        /// </summary>
        public Fix64 attackDamageRange;
        
        /// <summary>
        /// 攻击伤害判定角度（扇形角度，弧度制）
        /// </summary>
        public Fix64 attackDamageAngle;
        
        public ZombieAIComponent(FixVector2 targetPosition, Fix64 moveSpeed)
        {
            this.targetPosition = targetPosition;
            this.moveSpeed = moveSpeed;
            this.currentPath = new List<FixVector2>();
            this.currentPathIndex = 0;
            this.pathfindingCooldown = 0;
            this.state = ZombieState.Chase;
            this.attackDirection = FixVector2.Zero;
            this.attackWindupTimer = 0;
            this.attackCooldownTimer = 0;
            this.attackDetectionCooldown = 0;
            this.attackRange = (Fix64)2.0f;
            this.attackDamage = 10;
            this.attackWindupFrames = 10;
            this.attackCooldownFrames = 20;
            this.attackDamageRange = (Fix64)1.5f;
            this.attackDamageAngle = (Fix64)(60.0 * System.Math.PI / 180.0); // 60度
        }
        
        public object Clone()
        {
            return new ZombieAIComponent
            {
                targetPosition = this.targetPosition,
                moveSpeed = this.moveSpeed,
                currentPath = this.currentPath != null ? new List<FixVector2>(this.currentPath) : new List<FixVector2>(),
                currentPathIndex = this.currentPathIndex,
                pathfindingCooldown = this.pathfindingCooldown,
                state = this.state,
                attackDirection = this.attackDirection,
                attackWindupTimer = this.attackWindupTimer,
                attackCooldownTimer = this.attackCooldownTimer,
                attackDetectionCooldown = this.attackDetectionCooldown,
                attackRange = this.attackRange,
                attackDamage = this.attackDamage,
                attackWindupFrames = this.attackWindupFrames,
                attackCooldownFrames = this.attackCooldownFrames,
                attackDamageRange = this.attackDamageRange,
                attackDamageAngle = this.attackDamageAngle
            };
        }
        
        public override string ToString()
        {
            return $"{GetType().Name}: state={state}, target={targetPosition}, pathIndex={currentPathIndex}/{currentPath?.Count ?? 0}";
        }
    }
}

