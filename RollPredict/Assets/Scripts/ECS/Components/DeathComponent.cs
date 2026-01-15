using System;

namespace Frame.ECS
{
    /// <summary>
    /// 死亡组件：标记实体已死亡，等待处理
    /// 
    /// 使用场景：
    /// - 当实体HP <= 0时，添加此组件作为标记
    /// - DeathSystem 会处理所有有 DeathComponent 的实体
    /// - 根据实体类型执行相应的死亡逻辑
    /// 
    /// 设计说明：
    /// - 这是一个标记组件（Tag Component），不包含数据
    /// - 使用标记组件可以高效地查询需要处理的实体
    /// </summary>
    [Serializable]
    public struct DeathComponent : IComponent
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


