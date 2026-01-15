using System.Collections.Generic;
using Frame.FixMath;
using Proto;

namespace Frame.ECS
{
    /// <summary>
    /// 僵直系统：统一处理所有实体的僵直状态
    /// 
    /// 功能：
    /// - 处理所有有StiffComponent的实体的僵直计时器
    /// - 每帧递减僵直计时器
    /// - 计时器归零后自动清除僵直状态
    /// 
    /// 使用场景：
    /// - 玩家受伤僵直
    /// - 僵尸受伤僵直
    /// - Boss受伤僵直
    /// - 任何需要僵直状态的实体
    /// </summary>
    public class StiffSystem : ISystem
    {
        public void Execute(World world, List<FrameData> inputs)
        {
            // 处理所有有StiffComponent的实体
            foreach (var (entity, stiff) in world.GetEntitiesWithComponents<StiffComponent>())
            {
                if (stiff.stiffTimer > 0)
                {
                    var updatedStiff = stiff;
                    updatedStiff.stiffTimer--;
                    world.AddComponent(entity, updatedStiff);
                }
            }
        }
    }
}

