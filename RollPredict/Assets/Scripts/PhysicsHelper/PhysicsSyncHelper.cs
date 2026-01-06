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
/// </summary>
public static class PhysicsSyncHelper
{
    /// <summary>
    /// 物理体ID到RigidBody2D的映射
    /// 在物理体创建时注册，销毁时移除
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
    /// 从GameState保存物理体状态到GameState
    /// 在保存快照前调用，将当前Unity对象的状态保存到GameState
    /// </summary>
    public static void SaveToGameState(GameState gameState)
    {   
        var allBodies =PhysicsWorld2DComponent.Instance.World.GetAllBodies();
        gameState.physicsBodies.Clear();
        // state = entity
        foreach (var body in allBodies)
        {
            if (body != null && body.id > 0)
            {
                gameState.physicsBodies[body.id].bodyId = body.id;
                gameState.physicsBodies[body.id].position= body.Position;
                gameState.physicsBodies[body.id].velocity = body.Velocity;
            }
        }
    }

    /// <summary>
    /// 从GameState恢复物理体状态到Unity对象
    /// 在回滚时调用，将GameState中的状态应用到Unity对象
    /// </summary>
    public static void RestoreFromGameState(GameState gameState)
    {

        // 遍历GameState中的所有物理体状态，恢复它们
        foreach (var (id,bodyState) in gameState.physicsBodies)
        {
            
            // 通过ID找到对应的RigidBody2D对象
            if (bodyIdToRigidBody.TryGetValue(id, out RigidBody2D body))
            {
                    // 恢复位置和速度
                    body.Position = bodyState.position;
                    body.Velocity = bodyState.velocity;
                    // 标记为脏，需要更新四叉树
                    body.QuadTreeDirty = true;
            }
            else
            {
                // 按道理说需要恢复？
            }
        }
    }


}

