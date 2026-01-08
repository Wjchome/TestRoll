using System;

namespace Frame.ECS
{
    /// <summary>
    /// Component接口：所有Component必须实现此接口
    /// 
    /// Component设计原则：
    /// 1. 纯数据结构：只包含数据，不包含逻辑
    /// 2. 可序列化：支持深拷贝和序列化
    /// 3. 值类型优先：使用struct而非class（如果可能）
    /// 
    /// 在预测回滚中：
    /// - Component就是状态数据，可以直接序列化
    /// - 状态快照就是Component的快照
    /// - 回滚时直接替换Component数据即可
    /// </summary>
    public interface IComponent:ICloneable
    {

    }
}

