using Frame.FixMath;

namespace Frame.Physics3D
{
    /// <summary>
    /// 碰撞接触信息（3D）
    /// </summary>
    public struct Contact3D
    {
        /// <summary>
        /// 接触点（世界坐标）
        /// </summary>
        public FixVector3 Point;

        /// <summary>
        /// 碰撞法向量（从A指向B，已归一化）
        /// </summary>
        public FixVector3 Normal;

        /// <summary>
        /// 穿透深度（正值表示重叠）
        /// </summary>
        public Fix64 Penetration;
    }
}

