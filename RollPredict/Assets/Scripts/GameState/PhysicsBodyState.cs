using System;
using System.Collections.Generic;
using Frame.FixMath;

/// <summary>
/// 物理体状态数据（用于预测回滚）
/// 只存储需要回滚的数据，不包含Unity对象引用
/// 
/// 商业游戏做法参考：
/// 1. 存储完整的物理状态（位置、速度、碰撞状态）以确保回滚后能完全重现物理模拟
/// 2. 碰撞状态（LastCollidingBodyIds）用于正确计算Enter/Stay/Exit事件
/// 3. 只存储ID而非引用，保证状态可序列化和确定性
/// </summary>
[Serializable]
public class PhysicsBodyState : ICloneable
{
    /// <summary>
    /// 物理体ID（用于映射到实际的RigidBody2D对象）
    /// </summary>
    public int bodyId;

    /// <summary>
    /// 位置（世界坐标）
    /// </summary>
    public FixVector2 position;

    /// <summary>
    /// 速度
    /// </summary>
    public FixVector2 velocity;

    /// <summary>
    /// 上一帧碰撞的物理体ID列表（用于计算Enter/Stay/Exit事件）
    /// 重要：只存储ID，不存储引用，确保状态可序列化和确定性
    /// 
    /// 在回滚时，需要恢复这个列表，以便物理系统能正确计算：
    /// - Enter: CurrentRigidBody2D 中存在但 LastRigidBody2D 中不存在的
    /// - Stay: CurrentRigidBody2D 和 LastRigidBody2D 中都存在的
    /// - Exit: LastRigidBody2D 中存在但 CurrentRigidBody2D 中不存在的
    /// </summary>
    public List<int> lastCollidingBodyIds;

    public PhysicsBodyState(int bodyId, FixVector2 position, FixVector2 velocity)
    {
        this.bodyId = bodyId;
        this.position = position;
        this.velocity = velocity;
        this.lastCollidingBodyIds = new List<int>();
    }

    public PhysicsBodyState(int bodyId, FixVector2 position, FixVector2 velocity, List<int> lastCollidingBodyIds)
    {
        this.bodyId = bodyId;
        this.position = position;
        this.velocity = velocity;
        this.lastCollidingBodyIds = lastCollidingBodyIds != null ? new List<int>(lastCollidingBodyIds) : new List<int>();
    }

    /// <summary>
    /// 深拷贝
    /// </summary>
    public PhysicsBodyState Clone()
    {
        return new PhysicsBodyState(this.bodyId, this.position, this.velocity, this.lastCollidingBodyIds);
    }

    object ICloneable.Clone()
    {
        return Clone();
    }
}


