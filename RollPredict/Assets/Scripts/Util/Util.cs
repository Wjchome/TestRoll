using Frame.FixMath;
using Proto;


public static class Util
{
    /// <summary>
    /// 将输入方向转换为移动向量（FixVector2）
    /// 支持8个方向，斜向移动需要归一化
    /// </summary>
    public static FixVector2 GetMovementDirection(InputDirection direction)
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
                return (FixVector2.Up + FixVector2.Left) * diagonalFactor;

            case InputDirection.DirectionUpRight:
                return (FixVector2.Up + FixVector2.Right) * diagonalFactor;

            case InputDirection.DirectionDownLeft:
                return (FixVector2.Down + FixVector2.Left) * diagonalFactor;

            case InputDirection.DirectionDownRight:
                return (FixVector2.Down + FixVector2.Right) * diagonalFactor;

            case InputDirection.DirectionNone:
            default:
                return FixVector2.Zero;
        }
    }
}