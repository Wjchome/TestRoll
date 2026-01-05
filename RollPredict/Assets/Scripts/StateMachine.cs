using System.Collections.Generic;
using Frame.FixMath;
using Proto;

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
    /// </summary>
    /// <param name="currentState">当前帧状态 State(n)</param>
    /// <param name="inputs">当前帧所有玩家的输入 Input(n)</param>
    /// <returns>下一帧状态 State(n+1)</returns>
    public static GameState Execute(GameState currentState, Dictionary<int, InputDirection> inputs)
    {
        // 创建新状态（深拷贝）
        GameState nextState = currentState.Clone();
        nextState.frameNumber = currentState.frameNumber + 1;

        // 对每个玩家应用输入
        foreach (var kvp in inputs)
        {
            int playerId = kvp.Key;
            InputDirection direction = kvp.Value;

            // 获取或创建玩家状态
            PlayerState playerState = nextState.GetOrCreatePlayer(playerId);

            // 根据输入更新玩家位置
            UpdatePlayerPosition(playerState, direction);
        }

        return nextState;
    }

    /// <summary>
    /// 根据输入方向更新玩家位置（8个方向）
    /// </summary>
    private static void UpdatePlayerPosition(PlayerState playerState, InputDirection direction)
    {
        FixVector3 movement = FixVector3.Zero;
        
        // 斜向移动的速度需要归一化（保持与正交方向相同的速度）
        // 使用 0.707 作为 sqrt(2)/2 的近似值
        Fix64 diagonalSpeed = PlayerSpeed * (Fix64)0.7071067811865476f; // sqrt(2)/2

        switch (direction)
        {
            case InputDirection.DirectionUp:
                movement = FixVector3.Up * PlayerSpeed;
                break;
            case InputDirection.DirectionDown:
                movement = FixVector3.Down * PlayerSpeed;
                break;
            case InputDirection.DirectionLeft:
                movement = FixVector3.Left * PlayerSpeed;
                break;
            case InputDirection.DirectionRight:
                movement = FixVector3.Right * PlayerSpeed;
                break;
            case InputDirection.DirectionUpLeft:
                // 左上：上 + 左
                movement = (FixVector3.Up + FixVector3.Left) * diagonalSpeed;
                break;
            case InputDirection.DirectionUpRight:
                // 右上：上 + 右
                movement = (FixVector3.Up + FixVector3.Right) * diagonalSpeed;
                break;
            case InputDirection.DirectionDownLeft:
                // 左下：下 + 左
                movement = (FixVector3.Down + FixVector3.Left) * diagonalSpeed;
                break;
            case InputDirection.DirectionDownRight:
                // 右下：下 + 右
                movement = (FixVector3.Down + FixVector3.Right) * diagonalSpeed;
                break;
            case InputDirection.DirectionNone:
                // 无输入，不移动
                break;
        }

        playerState.position += movement;
    }
}

