using System;
using Frame.FixMath;

namespace Frame.ECS
{
    /// <summary>
    /// 血量组件：通用血量组件，任何实体都可以使用
    /// 
    /// 使用场景：
    /// - 玩家血量
    /// - 僵尸血量
    /// - Boss血量
    /// - 任何需要血量的实体
    /// </summary>
    [Serializable]
    public struct HPComponent : IComponent
    {
        /// <summary>
        /// 当前血量
        /// </summary>
        public int HP;
        
        /// <summary>
        /// 最大血量
        /// </summary>
        public int maxHP;

        public HPComponent(int maxHP)
        {
            this.maxHP = maxHP;
            this.HP = maxHP;
        }
        

        public object Clone()
        {
            return this;
        }

        public override string ToString()
        {
            return $"{GetType().Name}: HP={HP}/{maxHP}";
        }
    }
}


