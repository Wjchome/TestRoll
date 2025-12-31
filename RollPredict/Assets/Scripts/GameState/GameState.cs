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
    /// 当前帧号
    /// </summary>
    public long frameNumber;

    public GameState()
    {
        players = new Dictionary<int, PlayerState>();
        frameNumber = 0;
    }

    public GameState(long frameNumber)
    {
        players = new Dictionary<int, PlayerState>();
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
        return newState;
    }

    /// <summary>
    /// 获取玩家状态，如果不存在则创建
    /// </summary>
    public PlayerState GetOrCreatePlayer(int playerId)
    {
        if (!players.ContainsKey(playerId))
        {
            players[playerId] = new PlayerState(playerId, FixVector3.Zero);
        }
        return players[playerId];
    }
}

