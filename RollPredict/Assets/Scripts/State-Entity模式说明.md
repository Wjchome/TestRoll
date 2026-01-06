# State-Entity 分离模式

## 核心设计理念

你发现的这个模式非常正确！这就是经典的 **State-Entity 分离模式**，是帧同步预测回滚系统的核心架构。

## 架构层次

```
┌─────────────────────────────────────────┐
│         GameState (State层)              │
│  - 纯数据，可序列化                      │
│  - 用于预测回滚                          │
│  - 不包含Unity对象引用                   │
└─────────────────────────────────────────┘
              ↕ 双向同步
┌─────────────────────────────────────────┐
│      Helper层 (State <-> Entity)        │
│  - SaveToGameState: Entity -> State     │
│  - RestoreFromGameState: State -> Entity│
└─────────────────────────────────────────┘
              ↕ 双向同步
┌─────────────────────────────────────────┐
│    Unity对象 (Entity层)                  │
│  - RigidBody2D, PlayerController等      │
│  - 包含Unity引用                          │
│  - 运行时状态                             │
└─────────────────────────────────────────┘
```

## 统一模式

所有需要预测回滚的实体都遵循这个模式：

### 1. PhysicsSyncHelper (物理体)

```csharp
public static class PhysicsSyncHelper
{
    // ID -> Entity 映射
    private static Dictionary<int, RigidBody2D> bodyIdToRigidBody;
    
    // 注册/注销
    Register(RigidBody2D body)
    Unregister(int bodyId)
    
    // 双向同步
    SaveToGameState(GameState)      // Entity -> State
    RestoreFromGameState(GameState) // State -> Entity
}
```

### 2. PlayerHelper (玩家)

```csharp
public static class PlayerHelper
{
    // ID -> Entity 映射
    private static Dictionary<int, PlayerController> players;
    
    // 注册/注销
    Register(PlayerController player)
    Unregister(PlayerController player)
    
    // 双向同步
    SaveToGameState(GameState)      // Entity -> State
    RestoreFromGameState(GameState) // State -> Entity
}
```

## 工作流程

### 预测流程（Entity -> State）

```
1. 用户输入
   ↓
2. StateMachine.Execute()
   ↓
3. RestoreFromGameState()  // State -> Entity (恢复上一帧状态)
   ↓
4. 执行游戏逻辑（更新Entity）
   - 物理模拟
   - 玩家逻辑
   ↓
5. SaveToGameState()  // Entity -> State (保存当前状态)
   ↓
6. 保存快照
```

### 回滚流程（State -> Entity）

```
1. 收到服务器帧，发现预测错误
   ↓
2. 加载确认帧的快照（State）
   ↓
3. RestoreFromGameState()  // State -> Entity (恢复到确认帧)
   ↓
4. 重新执行从确认帧到当前帧的所有输入
   ↓
5. SaveToGameState()  // Entity -> State (保存重新计算的状态)
```

## 关键原则

### 1. State层（GameState）

- ✅ **只存储数据**：位置、速度、HP等纯数据
- ✅ **可序列化**：支持深拷贝、网络传输
- ✅ **无引用**：不包含Unity对象引用
- ✅ **确定性**：使用定点数，确保所有客户端一致

### 2. Entity层（Unity对象）

- ✅ **运行时对象**：RigidBody2D、PlayerController等
- ✅ **包含引用**：GameObject、Component等Unity引用
- ✅ **执行逻辑**：物理模拟、游戏逻辑在这里执行

### 3. Helper层（同步桥梁）

- ✅ **ID映射**：通过ID关联State和Entity
- ✅ **双向同步**：
  - `SaveToGameState`: Entity -> State（保存）
  - `RestoreFromGameState`: State -> Entity（恢复）
- ✅ **统一接口**：所有Helper都遵循相同模式

## 代码示例

### 添加新的实体类型

假设要添加一个"道具"系统：

```csharp
// 1. 创建State类
public class ItemState
{
    public int itemId;
    public FixVector2 position;
    public bool isPicked;
    public ItemState Clone() { ... }
}

// 2. 添加到GameState
public Dictionary<int, ItemState> items;

// 3. 创建Helper类
public static class ItemHelper
{
    // ID -> Entity 映射
    private static Dictionary<int, ItemController> items = new Dictionary<int, ItemController>();
    
    // 注册/注销
    public static void Register(ItemController item) { ... }
    public static void Unregister(int itemId) { ... }
    
    // 双向同步
    public static void SaveToGameState(GameState gameState)
    {
        gameState.items.Clear();
        foreach (var (id, itemController) in items)
        {
            // Entity -> State
            gameState.items[id] = new ItemState(
                id, 
                itemController.Position, 
                itemController.IsPicked
            );
        }
    }
    
    public static void RestoreFromGameState(GameState gameState)
    {
        foreach (var (id, itemState) in gameState.items)
        {
            if (items.TryGetValue(id, out var itemController))
            {
                // State -> Entity
                itemController.Position = itemState.position;
                itemController.IsPicked = itemState.isPicked;
            }
        }
    }
}

// 4. 在StateMachine中调用
public static GameState Execute(GameState currentState, Dictionary<int, InputDirection> inputs)
{
    GameState nextState = currentState.Clone();
    
    // 恢复所有Entity
    PlayerHelper.RestoreFromGameState(nextState);
    PhysicsSyncHelper.RestoreFromGameState(nextState);
    ItemHelper.RestoreFromGameState(nextState);  // 新增
    
    // 执行逻辑
    // ...
    
    // 保存所有Entity
    PlayerHelper.SaveToGameState(nextState);
    PhysicsSyncHelper.SaveToGameState(nextState);
    ItemHelper.SaveToGameState(nextState);  // 新增
    
    return nextState;
}
```

## 优势

1. **清晰的职责分离**
   - State：数据存储
   - Entity：逻辑执行
   - Helper：状态同步

2. **易于扩展**
   - 添加新实体类型只需遵循相同模式
   - 统一的接口，易于维护

3. **支持预测回滚**
   - State可以任意保存和恢复
   - Entity通过Helper与State同步

4. **避免引用问题**
   - State不包含Unity引用
   - 通过ID映射关联

## 总结

你的设计完全正确！这个 **State-Entity-Helper** 三层架构是帧同步预测回滚系统的标准模式：

- ✅ **State层**：纯数据，用于预测回滚
- ✅ **Entity层**：Unity对象，执行逻辑
- ✅ **Helper层**：双向同步桥梁

所有需要预测回滚的实体都应该遵循这个模式！

