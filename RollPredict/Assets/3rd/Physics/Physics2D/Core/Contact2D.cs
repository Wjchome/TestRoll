using Frame.FixMath;

namespace Frame.Physics2D
{
    /// <summary>
    /// 碰撞接触信息
    /// </summary>
    public struct Contact2D
    {
        /// <summary>
        /// 接触点（世界坐标）
        /// </summary>
        public FixVector2 Point;

        /// <summary>
        /// 碰撞法向量（从A指向B，已归一化）
        /// </summary>
        public FixVector2 Normal;

        /// <summary>
        /// 穿透深度（正值表示重叠）
        /// </summary>
        public Fix64 Penetration;
        
    }
}


