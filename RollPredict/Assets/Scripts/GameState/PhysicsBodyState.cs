using System;
using Frame.FixMath;

/// <summary>
/// 物理体状态数据（用于预测回滚）
/// 只存储需要回滚的数据，不包含Unity对象引用
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

    public PhysicsBodyState(int bodyId, FixVector2 position, FixVector2 velocity)
    {
        this.bodyId = bodyId;
        this.position = position;
        this.velocity = velocity;
    }

    /// <summary>
    /// 深拷贝
    /// </summary>
    public PhysicsBodyState Clone()
    {
        return new PhysicsBodyState(this.bodyId, this.position, this.velocity);
    }

    object ICloneable.Clone()
    {
        return Clone();
    }
}


