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
    public int HP;
    public PlayerState(int playerId, int HP)
    {
        this.playerId = playerId;
        this.HP = HP;
    }

    public PlayerState Clone()
    {
        return new PlayerState(this.playerId, this.HP);
    }
}

