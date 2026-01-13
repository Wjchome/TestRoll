using System;
using System.Collections.Generic;
using Frame.FixMath;

namespace Frame.ECS
{

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
        
        public ZombieAIComponent(FixVector2 targetPosition, Fix64 moveSpeed)
        {
            this.targetPosition = targetPosition;
            this.moveSpeed = moveSpeed;
            this.currentPath = new List<FixVector2>();
            this.currentPathIndex = 0;
            this.pathfindingCooldown = 0;
        }
        
        public object Clone()
        {
            return new ZombieAIComponent
            {
                targetPosition = this.targetPosition,
                moveSpeed = this.moveSpeed,
                currentPath = this.currentPath != null ? new List<FixVector2>(this.currentPath) : new List<FixVector2>(),
                currentPathIndex = this.currentPathIndex,
                pathfindingCooldown = this.pathfindingCooldown
            };
        }
        
        public override string ToString()
        {
            return $"{GetType().Name}: target={targetPosition}, pathIndex={currentPathIndex}/{currentPath?.Count ?? 0}";
        }
    }
}

