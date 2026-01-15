using System;
using System.Collections.Generic;
using System.Linq;
using Frame.ECS;
using Frame.FixMath;
using Proto;

namespace Frame.ECS
{
    /// <summary>
    /// ECS版本的状态机
    /// 实现 State(n+1) = StateMachine(State(n), Input(n))
    /// 
    /// 设计：
    /// - 使用ECS World存储所有游戏状态
    /// - 输入处理：移动玩家、发射子弹
    /// - 系统更新：更新子弹位置等
    /// </summary>
    public static class ECSStateMachine
    {
        private static OrderedDictionary<Type, ISystem> _systems = new OrderedDictionary<Type, ISystem>();
        private static bool _initialized = false;

        /// <summary>
        /// 注册一个System到状态机
        /// </summary>
        /// <param name="system">要注册的System</param>
        /// <returns>是否注册成功（如果已存在则返回false）</returns>
        public static bool RegisterSystem(ISystem system)
        {
            if (system == null)
            {
                UnityEngine.Debug.LogError("[ECSStateMachine] Cannot register null system");
                return false;
            }

            var type = system.GetType();
            if (_systems.ContainsKey(type))
            {
                UnityEngine.Debug.LogWarning($"[ECSStateMachine] System {system.GetType().Name} already registered");
                return false;
            }

            _systems[type] = system;
            UnityEngine.Debug.Log($"[ECSStateMachine] Registered system: {system.GetType().Name}");
            return true;
        }

        // /// <summary>
        // /// 取消注册一个System
        // /// </summary>
        // /// <param name="system">要取消注册的System</param>
        // /// <returns>是否成功取消注册</returns>
        // public static bool UnregisterSystem(ISystem system)
        // {
        //     if (system == null)
        //         return false;
        //
        //     bool removed = _systems.Remove(system);
        //     if (removed)
        //     {
        //         UnityEngine.Debug.Log($"[ECSStateMachine] Unregistered system: {system.GetType().Name}");
        //     }
        //
        //     return removed;
        // }

        /// <summary>
        /// 清空所有已注册的System
        /// </summary>
        public static void ClearSystems()
        {
            _systems.Clear();
            _initialized = false;
            UnityEngine.Debug.Log("[ECSStateMachine] Cleared all systems");
        }

        /// <summary>
        /// 获取指定类型的System
        /// </summary>
        /// <typeparam name="T">System类型</typeparam>
        /// <returns>System实例，如果不存在则返回null</returns>
        public static T GetSystem<T>() where T : class, ISystem
        {
            return _systems[typeof(T)] as T;
        }

        /// <summary>
        /// 获取已注册的System数量
        /// </summary>
        public static int GetSystemCount()
        {
            return _systems.Count;
        }

        /// <summary>
        /// 初始化默认System（游戏启动时调用一次）
        /// </summary>
        public static void InitializeDefaultSystems()
        {
            if (_initialized)
            {
                UnityEngine.Debug.LogWarning("[ECSStateMachine] Systems already initialized");
                return;
            }

            ClearSystems();


            RegisterSystem(new PlayerToggleSystem());
            RegisterSystem(new PlayerCooldownSystem());
            RegisterSystem(new StiffSystem()); // 通用僵直系统（处理所有实体的僵直状态）
            RegisterSystem(new DeathSystem()); // 死亡系统（统一处理所有实体的死亡逻辑）
            RegisterSystem(new PlayerMoveSystem());
            RegisterSystem(new PlayerShootSystem());
            RegisterSystem(new PlayerPlaceWallSystem());
            RegisterSystem(new BulletCheckSystem());
            RegisterSystem(new PhysicsSystem());
            RegisterSystem(new ZombieSpawnSystem());
            RegisterSystem(new ZombieAISystem()); // AI系统根据状态决定是否移动

            // 可以继续添加其他System：
            // RegisterSystem(new CollisionDetectionSystem());
            // RegisterSystem(new HealthSystem());
            // RegisterSystem(new ScoreSystem());

            _initialized = true;
            UnityEngine.Debug.Log($"[ECSStateMachine] Initialized {_systems.Count} default systems");
        }

        /// <summary>
        /// 状态机核心函数：根据当前状态和输入计算下一帧状态
        /// State(n+1) = StateMachine(State(n), Input(n+1))
        /// </summary>
        /// <param name="world">当前帧的World状态 State(n)</param>
        /// <param name="inputs">当前帧所有玩家的输入 Input(n)</param>
        /// <returns>下一帧的World状态 State(n+1)</returns>
        public static World Execute(World world, List<FrameData> inputs)
        {
            // 如果没有初始化，自动初始化默认System
            if (!_initialized)
            {
                InitializeDefaultSystems();
            }

            // 按顺序执行所有System
            foreach (var (_,system) in _systems)
            {
                system.Execute(world, inputs);
            }

            return world;
        }
    }
}