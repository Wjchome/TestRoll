using System;
using Frame.FixMath;

namespace Frame.ECS
{
    /// <summary>
    /// 油桶组件：可被放置、可被摧毁、爆炸的物体
    /// 
    /// 特性：
    /// - 有血量，可被子弹伤害
    /// - 血量归零时爆炸
    /// - 爆炸造成范围伤害
    /// - 类似《僵尸危机3》的油桶
    /// 
    /// 使用场景：
    /// - 玩家放置陷阱
    /// - 战术道具
    /// - 环境交互物
    /// </summary>
    [Serializable]
    public struct BarrelComponent : IComponent
    {
        public object Clone()
        {
            return this;
        }

        public override string ToString()
        {
            return $"{GetType().Name}";
        }
    }
}
