using System;
using System.Collections.Generic;
using UnityEngine;
using Proto;

/// <summary>
/// 游戏状态快照
/// 用于保存每一帧的游戏状态，支持回滚
/// </summary>
[Serializable]
public class GameStateSnapshot
{
    public long frameNumber;
    public Dictionary<int, PlayerState> playerStates;

    public GameStateSnapshot(long frameNumber)
    {
        this.frameNumber = frameNumber;
        this.playerStates = new Dictionary<int, PlayerState>();
    }

    public GameStateSnapshot Clone()
    {
        var snapshot = new GameStateSnapshot(this.frameNumber);
        foreach (var kvp in this.playerStates)
        {
            snapshot.playerStates[kvp.Key] = kvp.Value.Clone();
        }
        return snapshot;
    }
}

/// <summary>
/// 玩家状态
/// </summary>
[Serializable]
public class PlayerState
{
    public int playerId;
    public Vector3 position;
    public Quaternion rotation;

    public PlayerState(int playerId, Vector3 position, Quaternion rotation)
    {
        this.playerId = playerId;
        this.position = position;
        this.rotation = rotation;
    }

    public PlayerState Clone()
    {
        return new PlayerState(this.playerId, this.position, this.rotation);
    }
}

