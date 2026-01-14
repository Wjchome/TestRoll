using System;
using Frame.FixMath;
using Frame.Physics2D;

namespace Frame.ECS
{
    /// <summary>
    /// 碰撞形状类型
    /// </summary>
    public enum ShapeType : byte
    {
        None = 0,
        Circle = 1,
        Box = 2
    }

    /// <summary>
    /// 碰撞形状组件：存储碰撞形状信息（只支持不旋转的矩形和圆形）
    /// </summary>
    [Serializable]
    public struct CollisionShapeComponent : IComponent
    {
        /// <summary>
        /// 形状类型
        /// </summary>
        public ShapeType shapeType;

        /// <summary>
        /// 半径（仅用于Circle）
        /// </summary>
        public Fix64 radius;

        /// <summary>
        /// 尺寸（宽度、高度，仅用于Box）
        /// </summary>
        public FixVector2 size;

        public CollisionShapeComponent(ShapeType shapeType, Fix64 radius , FixVector2 size = default)
        {
            this.shapeType = shapeType;
            this.radius = radius;
            this.size = size;
        }

        /// <summary>
        /// 创建圆形形状
        /// </summary>
        public static CollisionShapeComponent CreateCircle(Fix64 radius)
        {
            if (radius <= Fix64.Zero)
                throw new ArgumentException("半径必须大于0", nameof(radius));
            
            return new CollisionShapeComponent(ShapeType.Circle, radius, default);
        }

        /// <summary>
        /// 创建矩形形状（不旋转）
        /// </summary>
        public static CollisionShapeComponent CreateBox(Fix64 width, Fix64 height)
        {
            if (width <= Fix64.Zero || height <= Fix64.Zero)
                throw new ArgumentException("宽度和高度必须大于0", nameof(width));
            
            return new CollisionShapeComponent(ShapeType.Box, Fix64.Zero, new FixVector2(width, height));
        }

        /// <summary>
        /// 获取AABB边界（用于宽相位碰撞检测）
        /// </summary>
        public FixRect GetBounds(FixVector2 position)
        {
            if (shapeType == ShapeType.Circle)
            {
                return new FixRect(
                    position.x - radius,
                    position.y - radius,
                    radius * Fix64.Two,
                    radius * Fix64.Two
                );
            }
            else if (shapeType == ShapeType.Box)
            {
                Fix64 halfWidth = size.x / Fix64.Two;
                Fix64 halfHeight = size.y / Fix64.Two;
                return new FixRect(
                    position.x - halfWidth,
                    position.y - halfHeight,
                    size.x,
                    size.y
                );
            }
            return default;
        }

        public object Clone()
        {
            return this;
        }

        public override string ToString()
        {
            if (shapeType == ShapeType.Circle)
                return $"{GetType().Name}: Circle(radius={radius})";
            else if (shapeType == ShapeType.Box)
                return $"{GetType().Name}: Box(size={size})";
            return $"{GetType().Name}: None";
        }
    }
}


