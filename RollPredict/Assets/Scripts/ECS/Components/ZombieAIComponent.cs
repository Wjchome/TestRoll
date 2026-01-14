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
        Idle = 0,           // 空闲（未找到目标）
        Chase = 1,          // 追逐（寻路移动）
        AttackWindup = 2,   // 攻击前摇
        Attack = 3,         // 攻击中（伤害判定帧）
        AttackCooldown = 4 , // 攻击后摇
        StraightChase = 5, //直线追逐（找不到目标时直线行走）
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

        
        public ZombieAIComponent(FixVector2 targetPosition, Fix64 moveSpeed)
        {
            this.targetPosition = targetPosition;
            this.moveSpeed = moveSpeed;
            this.currentPath = new List<FixVector2>();
            this.currentPathIndex = 0;
            this.pathfindingCooldown = 0;
            this.state = ZombieState.Idle;
            this.attackDirection = FixVector2.Zero;
            this.attackWindupTimer = 0;
            this.attackCooldownTimer = 0;
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
            };
        }
        
        public override string ToString()
        {
            return $"{GetType().Name}: state={state}, target={targetPosition}, pathIndex={currentPathIndex}/{currentPath?.Count ?? 0}";
        }
    }
}

