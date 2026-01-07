using System.Collections.Generic;
using Frame.FixMath;
using Frame.Physics2D;
using UnityEngine;

/// <summary>
/// 状态同步辅助类
/// 负责在GameState（纯数据）和Unity对象（RigidBody2D等）之间同步状态
/// 
/// 设计原则：
/// 1. GameState只存储数据，不包含Unity对象引用
/// 2. 使用ID映射来关联数据和对象
/// 3. 回滚时从GameState恢复Unity对象状态
/// 4. 预测时从Unity对象保存状态到GameState
/// 
/// 重要：这里使用的ID是物理体ID（body.id），不是玩家ID（playerId）
/// - body.id: 由物理世界分配，用于标识物理体（1, 2, 3...）
/// - playerId: 由游戏逻辑分配，用于标识玩家（100, 200, 300...）
/// - GameState.physicsBodies 使用 body.id 作为Key
/// - 如果需要通过playerId查找物理体，需要通过PredictionRollbackManager.playerRigidBodys
/// </summary>
public static class PhysicsSyncHelper
{
    /// <summary>
    /// 物理体ID到RigidBody2D的映射
    /// Key: body.id（物理体ID，由物理世界分配）
    /// Value: RigidBody2D对象
    /// 
    /// 注意：这里的ID是物理体ID，不是玩家ID！
    /// 如果需要通过玩家ID查找，使用：PredictionRollbackManager.playerRigidBodys[playerId].Body.id
    /// </summary>
    private static Dictionary<int, RigidBody2D> bodyIdToRigidBody = new Dictionary<int, RigidBody2D>();

    /// <summary>
    /// 注册物理体（在物理体创建时调用）
    /// </summary>
    public static void Register(RigidBody2D body)
    {
        if (body != null && body.id > 0)
        {
            bodyIdToRigidBody[body.id] = body;
        }
    }

    /// <summary>
    /// 注销物理体（在物理体销毁时调用）
    /// </summary>
    public static void Unregister(int bodyId)
    {
        bodyIdToRigidBody.Remove(bodyId);
    }

    /// <summary>
    /// 从Entity保存状态到GameState
    /// 在保存快照前调用，将当前Unity对象的状态保存到GameState
    /// Entity -> State
    /// 
    /// 注意：使用 body.id 作为Key保存到 gameState.physicsBodies
    /// 这与 playerId 不同，物理体ID由物理世界分配
    /// 
    /// 重要：保存LastRigidBody2D的ID列表，用于回滚后正确计算Enter/Stay/Exit事件
    /// 注意：LastRigidBody2D在ProcessAllBody()之后会被更新为当前帧的碰撞列表，
    /// 所以这里保存的是"上一帧的碰撞列表"，用于下一帧计算Enter/Stay/Exit
    /// </summary>
    public static void SaveToGameState(GameState gameState)
    {   
        var allBodies = PhysicsWorld2DComponent.Instance.World.GetAllBodies();
        gameState.physicsBodies.Clear();
        // state = entity
        // 使用 body.id（物理体ID）作为Key，不是playerId
        foreach (var body in allBodies)
        {
            if (body != null && body.id > 0)
            {
                // 提取LastRigidBody2D中的ID列表（只存储ID，不存储引用）
                List<int> lastCollidingBodyIds = new List<int>();
                if (body.LastRigidBody2D != null)
                {
                    foreach (var collidingBody in body.LastRigidBody2D)
                    {
                        if (collidingBody != null && collidingBody.id > 0)
                        {
                            lastCollidingBodyIds.Add(collidingBody.id);
                        }
                    }
                }

                // 创建或更新状态
                // Key: body.id（物理体ID）
                gameState.physicsBodies[body.id] = new PhysicsBodyState(
                    body.id, 
                    body.Position, 
                    body.Velocity,
                    lastCollidingBodyIds
                );
            }
        }
    }

    /// <summary>
    /// 从GameState恢复状态到Entity
    /// 在回滚时调用，将GameState中的状态应用到Unity对象
    /// State -> Entity
    /// 
    /// 注意：使用 body.id（物理体ID）查找物理体，不是playerId
    /// 
    /// 重要：恢复LastRigidBody2D列表，确保回滚后物理系统能正确计算Enter/Stay/Exit事件
    /// 这是预测回滚的关键：必须恢复完整的物理状态，包括碰撞历史
    /// </summary>
    public static void RestoreFromGameState(GameState gameState)
    {
        // 遍历GameState中的所有物理体状态，恢复它们
        // gameState.physicsBodies 的Key是 body.id（物理体ID），不是playerId
        foreach (var (bodyId, bodyState) in gameState.physicsBodies)
        {
            // 通过物理体ID（body.id）找到对应的RigidBody2D对象
            if (bodyIdToRigidBody.TryGetValue(bodyId, out RigidBody2D body))
            {
                // entity = state
                // 恢复位置和速度
                body.Position = bodyState.position;
                body.Velocity = bodyState.velocity;
                // 标记为脏，需要更新四叉树
                body.QuadTreeDirty = true;

                // 恢复碰撞状态：重建LastRigidBody2D列表（通过ID查找对应的RigidBody2D对象）
                body.LastRigidBody2D.Clear();
                if (bodyState.lastCollidingBodyIds != null)
                {
                    foreach (var collidingBodyId in bodyState.lastCollidingBodyIds)
                    {
                        // 通过ID查找对应的RigidBody2D对象
                        if (bodyIdToRigidBody.TryGetValue(collidingBodyId, out RigidBody2D collidingBody))
                        {
                            body.LastRigidBody2D.Add(collidingBody);
                        }
                        // 如果找不到对应的物理体，说明该物理体已被销毁，跳过
                    }
                }

                // 清空当前帧的碰撞列表，让物理系统重新计算
                body.CurrentRigidBody2D.Clear();
                body.Enter.Clear();
                body.Stay.Clear();
                body.Exit.Clear();
            }
            // 如果Entity不存在，说明这个物理体已经被销毁，不需要恢复
        }
    }


}

