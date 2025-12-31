using System;
using System.Collections.Generic;
using Frame.FixMath;
using UnityEngine;
using Proto;

/// <summary>
/// 玩家状态
/// </summary>
[Serializable]
public class PlayerState
{
    public int playerId;
    public FixVector3 position;

    public PlayerState(int playerId, FixVector3 position)
    {
        this.playerId = playerId;
        this.position = position;
    }

    public PlayerState Clone()
    {
        return new PlayerState(this.playerId, this.position);
    }
}

