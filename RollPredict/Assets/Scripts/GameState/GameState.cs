using System;
using System.Collections.Generic;
using Frame.FixMath;
using UnityEngine;

/// <summary>
/// 统一的游戏状态类
/// 包含所有游戏状态数据，支持克隆和序列化
/// </summary>
[Serializable]
public class GameState
{
    /// <summary>
    /// 玩家状态字典 (playerId -> PlayerState)
    /// </summary>
    public Dictionary<int, PlayerState> players;

    /// <summary>
    /// 物理体状态字典 (bodyId -> PhysicsBodyState)
    /// 存储所有需要预测回滚的物理体状态（位置、速度等）
    /// 
    /// 重要：Key是物理体ID（body.id），不是玩家ID（playerId）！
    /// - body.id: 由物理世界分配（1, 2, 3...）
    /// - playerId: 由游戏逻辑分配（100, 200, 300...）
    /// - 如果需要通过playerId查找物理体，需要通过PredictionRollbackManager.playerRigidBodys
    /// </summary>
    public Dictionary<int, PhysicsBodyState> physicsBodies;

    /// <summary>
    /// 当前帧号
    /// </summary>
    public long frameNumber;

    public GameState()
    {
        players = new Dictionary<int, PlayerState>();
        physicsBodies = new Dictionary<int, PhysicsBodyState>();
        frameNumber = 0;
    }

    public GameState(long frameNumber)
    {
        players = new Dictionary<int, PlayerState>();
        physicsBodies = new Dictionary<int, PhysicsBodyState>();
        this.frameNumber = frameNumber;
    }

    /// <summary>
    /// 深拷贝游戏状态
    /// </summary>
    public GameState Clone()
    {
        var newState = new GameState(this.frameNumber);
        foreach (var kvp in this.players)
        {
            newState.players[kvp.Key] = kvp.Value.Clone();
        }
        foreach (var kvp in this.physicsBodies)
        {
            newState.physicsBodies[kvp.Key] = kvp.Value.Clone();
        }
        return newState;
    }

}

