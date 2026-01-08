using System;
using System.Collections.Generic;
using Frame.FixMath;

namespace Frame.ECS
{
    /// <summary>
    /// ECS版本的GameState：使用Component存储状态
    /// 
    /// 设计：
    /// - 状态快照 = 所有Component的快照
    /// - 使用Dictionary<Type, Dictionary<Entity, IComponent>>存储
    /// - 完全解耦，不包含任何Unity对象引用
    /// 
    /// 优势：
    /// 1. 完全解耦：只存储数据，不存储引用
    /// 2. 可序列化：所有Component都是可序列化的
    /// 3. 类型安全：通过泛型访问Component
    /// 4. 易于扩展：添加新Component类型不需要修改GameState
    /// </summary>
    [Serializable]
    public class ECSGameState
    {
        /// <summary>
        /// Component快照：Type -> (Entity -> Component)
        /// 存储所有Component的状态快照
        /// </summary>
        public OrderedDictionary<string, OrderedDictionary<int, IComponent>> componentSnapshots;

        /// <summary>
        /// 当前帧号
        /// </summary>
        public long frameNumber;

        public ECSGameState()
        {
            componentSnapshots = new OrderedDictionary<string, OrderedDictionary<int, IComponent>>();
            frameNumber = 0;
        }

        public ECSGameState(long frameNumber)
        {
            componentSnapshots = new OrderedDictionary<string, OrderedDictionary<int, IComponent>>();
            this.frameNumber = frameNumber;
        }

        /// <summary>
        /// 从World创建状态快照
        /// </summary>
        public static ECSGameState CreateSnapshot(World world, long frameNumber)
        {
            var state = new ECSGameState(frameNumber);
            
            // 获取World中所有Component的快照
            OrderedDictionary<Type, OrderedDictionary<Entity, IComponent>> snapshots = world.GetAllComponentSnapshots();
            
            // 转换为可序列化的格式
            foreach (var kvp in snapshots)
            {
                string componentTypeName = kvp.Key.FullName;
                OrderedDictionary<Entity, IComponent> componentDict = kvp.Value; // 已经是 OrderedDictionary<Entity, IComponent> 类型
                
                var serializableDict = new OrderedDictionary<int, IComponent>();
                foreach (var componentKvp in componentDict)
                {
                    // 只存储Entity ID和Component的克隆
                    serializableDict[componentKvp.Key.Id] = componentKvp.Value.Clone() as IComponent;
                }
                state.componentSnapshots[componentTypeName] = serializableDict;
            }
            
            return state;
        }

        /// <summary>
        /// 恢复World的状态
        /// </summary>
        public void RestoreToWorld(World world)
        {
            // 清空World
            world.Clear();
            
            // 恢复所有Component
            foreach (var kvp in componentSnapshots)
            {
                var componentTypeName = kvp.Key;
                var componentDict = kvp.Value;
                
                // 通过类型名查找Component类型
                var componentType = Type.GetType(componentTypeName);
                if (componentType == null)
                {
                    // 尝试在当前程序集中查找
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        componentType = assembly.GetType(componentTypeName);
                        if (componentType != null) break;
                    }
                }
                
                if (componentType != null && typeof(IComponent).IsAssignableFrom(componentType))
                {
                    // 恢复每个Entity的Component
                    foreach (var componentKvp in componentDict)
                    {
                        var entity = new Entity(componentKvp.Key);
                        var component = componentKvp.Value;
                        
                        // 使用反射添加Component
                        var method = typeof(World).GetMethod("AddComponent");
                        var genericMethod = method.MakeGenericMethod(componentType);
                        genericMethod.Invoke(world, new object[] { entity, component });
                    }
                }
            }
        }

        /// <summary>
        /// 深拷贝
        /// </summary>
        public ECSGameState Clone()
        {
            var newState = new ECSGameState(this.frameNumber);
            
            foreach (var kvp in this.componentSnapshots)
            {
                var newDict = new OrderedDictionary<int, IComponent>();
                foreach (var componentKvp in kvp.Value)
                {
                    newDict[componentKvp.Key] = componentKvp.Value.Clone() as IComponent;
                }
                newState.componentSnapshots[kvp.Key] = newDict;
            }
            
            return newState;
        }

        public override string ToString()
        {
            return componentSnapshots.ToString();
        }
    }
}

