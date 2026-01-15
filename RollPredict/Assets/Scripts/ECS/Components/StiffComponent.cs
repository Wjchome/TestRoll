using System;
using Frame.FixMath;

namespace Frame.ECS
{
    /// <summary>
    /// 僵直组件：通用僵直状态组件，任何实体都可以使用
    /// 
    /// 使用场景：
    /// - 玩家受伤僵直
    /// - 僵尸受伤僵直
    /// - Boss受伤僵直
    /// - 任何需要僵直状态的实体
    /// </summary>
    [Serializable]
    public struct StiffComponent : IComponent
    {
        /// <summary>
        /// 当前僵直计时器（帧数）
        /// </summary>
        public int stiffTimer;
        
        /// <summary>
        /// 僵直持续时间（帧数，配置值）
        /// </summary>
        public int stiffDuration;


        public StiffComponent(int duration = 10)
        {
            this.stiffDuration = duration ;
            this.stiffTimer = 0;
        }
        
        /// <summary>
        /// 是否处于僵直状态
        /// </summary>
        public bool IsStiff => stiffTimer > 0;
        

        public object Clone()
        {
            return this;
        }

        public override string ToString()
        {
            return $"{GetType().Name}: timer={stiffTimer}/{stiffDuration}";
        }
    }
}

