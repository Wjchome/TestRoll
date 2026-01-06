using System.Collections.Generic;
using Frame.FixMath;
using Frame.Physics2D;
using Proto;
using UnityEngine;

/// <summary>
/// 统一的状态机框架
/// 实现 State(n+1) = StateMachine(State(n), Input(n))
/// 预测和非预测都使用同一套逻辑
/// </summary>
public static class StateMachine
{
    /// <summary>
    /// 玩家移动速度（固定点）
    /// </summary>
    public static Fix64 PlayerSpeed = (Fix64)0.1f;

    /// <summary>
    /// 状态机核心函数：根据当前状态和输入计算下一帧状态
    /// State(n+1) = StateMachine(State(n), Input(n))
    /// 
    /// 执行流程：
    /// 1. 处理玩家输入（更新玩家位置）
    /// 2. 恢复物理体状态到Unity对象（如果需要）
    /// 3. 执行物理模拟
    /// 4. 保存物理体状态到GameState
    /// </summary>
    /// <param name="currentState">当前帧状态 State(n)</param>
    /// <param name="inputs">当前帧所有玩家的输入 Input(n)</param>
    /// <returns>下一帧状态 State(n+1)</returns>
    public static GameState Execute(GameState currentState, Dictionary<int, InputDirection> inputs)
    {
        // 创建新状态（深拷贝）
        GameState nextState = currentState.Clone();
        nextState.frameNumber = currentState.frameNumber + 1;

        // 1. 恢复所有Entity状态（State -> Entity）
        // 从GameState恢复玩家状态到Entity
        PlayerHelper.RestoreFromGameState(nextState);
        // 从GameState恢复物理体状态到Entity
        PhysicsSyncHelper.RestoreFromGameState(nextState);

        // 2. 执行游戏逻辑（更新Entity）
        // 2.1 处理玩家输入（如果需要，可以在这里处理）
        // 2.2 执行物理模拟（这会更新所有物理体的位置和速度）
        
        PhysicsWorld2DComponent.Instance.World.Update();
        

        // 3. 保存所有Entity状态到GameState（Entity -> State）
        // 保存玩家状态
        PlayerHelper.SaveToGameState(nextState);
        // 保存物理体状态
        PhysicsSyncHelper.SaveToGameState(nextState);

        return nextState;
    }
}