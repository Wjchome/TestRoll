using System;
using System.Collections.Generic;
using Frame.FixMath;

namespace Frame.ECS
{
    /// <summary>
    /// 流场组件：存储流场数据和更新状态
    /// 用于预测回滚系统，确保流场状态可以被正确快照和回滚
    /// 
    /// 注意：
    /// - 这是一个单例组件，应该只有一个Entity拥有此组件
    /// - 流场数据包含Dictionary，在Clone时需要注意深拷贝问题
    /// - 由于Dictionary的深拷贝复杂，流场数据在回滚时可能需要重新计算
    /// </summary>
    [Serializable]
    public struct FlowFieldComponent : IComponent
    {
        /// <summary>
        /// 流场更新冷却时间（帧数）
        /// </summary>
        public int updateCooldown;

        public Dictionary<GridNode, FixVector2> gradientField;

        public FlowFieldComponent(int updateCooldown, Dictionary<GridNode, FixVector2> gradientField)
        {
            this.updateCooldown = updateCooldown;
            this.gradientField = gradientField;
        }
   
        
        public object Clone()
        {
            return new FlowFieldComponent
            {
                updateCooldown = this.updateCooldown,
                gradientField = this.gradientField == null 
                    ? null
                    : new Dictionary<GridNode, FixVector2>(this.gradientField) // 原字典非null → 拷贝元素到新字典

            };
        }
        
        public override string ToString()
        {
            return $"{GetType().Name}: updateCooldown = {updateCooldown}, gradientField = {gradientField}";
        }
    }
}

