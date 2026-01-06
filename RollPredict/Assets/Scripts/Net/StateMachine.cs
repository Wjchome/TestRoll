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
        // 2.1 处理玩家输入：将输入方向转换为力并应用到物理体
        foreach (var (playerId, inputDirection) in inputs)
        {
            // 跳过无输入
            if (inputDirection == InputDirection.DirectionNone)
                continue;
            
            // 检查玩家是否有物理体
            if (!PredictionRollbackManager.Instance.playerRigidBodys.TryGetValue(playerId, out var rigidBodyComp))
                continue;
            
            if (rigidBodyComp == null || rigidBodyComp.Body == null)
                continue;
            
            var body = rigidBodyComp.Body;
            
            // 将输入方向转换为移动向量
            FixVector2 movementDirection = GetMovementDirection(inputDirection);
            
            // // 应用玩家输入到物理体
            // // 方案1：直接设置速度（推荐，适合玩家控制，每帧速度确定）
            // // 这样每帧的速度是固定的，不会因为连续输入而累加
            // body.Velocity = movementDirection * PlayerSpeed;
            
            //方案2：使用冲量（会累加速度，可能导致速度无限增长）
            FixVector2 impulse = movementDirection * PlayerSpeed * body.Mass;
            body.ApplyImpulse(impulse);
            
            // 方案3：使用力（会在物理更新时影响加速度，更真实但响应稍慢）
            // FixVector2 force = movementDirection * PlayerSpeed * body.Mass;
            // body.ApplyForce(force);
        }
        
        // 2.2 执行物理模拟（这会更新所有物理体的位置和速度）
        
            PhysicsWorld2DComponent.Instance.World.Update();
        
        

        // 3. 保存所有Entity状态到GameState（Entity -> State）
        // 保存玩家状态
        PlayerHelper.SaveToGameState(nextState);
        // 保存物理体状态
        PhysicsSyncHelper.SaveToGameState(nextState);

        return nextState;
    }

    /// <summary>
    /// 将输入方向转换为移动向量（FixVector2）
    /// 支持8个方向，斜向移动需要归一化
    /// </summary>
    /// <param name="direction">输入方向</param>
    /// <returns>移动向量（已归一化）</returns>
    private static FixVector2 GetMovementDirection(InputDirection direction)
    {
        // 斜向移动的归一化系数（sqrt(2)/2 ≈ 0.707）
        Fix64 diagonalFactor = (Fix64)0.7071067811865476m; // sqrt(2)/2
        
        switch (direction)
        {
            case InputDirection.DirectionUp:
                return FixVector2.Up;
                
            case InputDirection.DirectionDown:
                return FixVector2.Down;
                
            case InputDirection.DirectionLeft:
                return FixVector2.Left;
                
            case InputDirection.DirectionRight:
                return FixVector2.Right;
                
            case InputDirection.DirectionUpLeft:
                // 左上：上 + 左，需要归一化
                return (FixVector2.Up + FixVector2.Left) * diagonalFactor;
                
            case InputDirection.DirectionUpRight:
                // 右上：上 + 右，需要归一化
                return (FixVector2.Up + FixVector2.Right) * diagonalFactor;
                
            case InputDirection.DirectionDownLeft:
                // 左下：下 + 左，需要归一化
                return (FixVector2.Down + FixVector2.Left) * diagonalFactor;
                
            case InputDirection.DirectionDownRight:
                // 右下：下 + 右，需要归一化
                return (FixVector2.Down + FixVector2.Right) * diagonalFactor;
                
            case InputDirection.DirectionNone:
            default:
                return FixVector2.Zero;
        }
    }
}