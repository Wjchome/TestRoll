# 物理系统ECS改造方案

## 一、改造目标

将现有的物理系统（`Frame.Physics2D`）改造成符合ECS架构的物理系统，使其能够：
1. 与现有的ECS框架无缝集成
2. 支持预测回滚机制
3. 保持确定性（使用定点数）
4. 简化实现：只支持2D、不旋转的矩形和圆形

## 二、Unity ECS碰撞处理机制参考

### 2.1 Unity Physics的碰撞处理方式

Unity的ECS物理系统（Unity Physics）采用**事件组件（Event Component）**的方式处理碰撞：

#### 2.1.1 碰撞事件组件

Unity Physics会在碰撞发生时，自动创建临时的事件Entity，并添加相应的组件：

1. **`CollisionEvent`** - 普通碰撞事件
   - 当两个非触发器碰撞体发生碰撞时创建
   - 包含碰撞信息：EntityA、EntityB、碰撞点、法向量等
   - 是一个**临时组件**，只在碰撞发生的帧存在

2. **`TriggerEvent`** - 触发器事件
   - 当触发器与其他碰撞体发生交互时创建
   - 包含触发信息：EntityA、EntityB等
   - 同样是一个**临时组件**

#### 2.1.2 事件处理流程

```csharp
// Unity Physics的处理流程（简化版）

// 1. 物理系统执行碰撞检测
PhysicsSystem.Execute()
{
    // 检测碰撞...
    
    // 2. 当发生碰撞时，创建事件Entity并添加CollisionEvent组件
    if (collisionDetected)
    {
        Entity eventEntity = world.CreateEntity();
        world.AddComponent(eventEntity, new CollisionEvent
        {
            EntityA = entityA,
            EntityB = entityB,
            // ... 其他碰撞信息
        });
    }
}

// 3. 其他System查询CollisionEvent组件来处理碰撞
public class DamageSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // 查询所有有CollisionEvent的Entity
        Entities
            .WithAll<CollisionEvent>()
            .ForEach((Entity entity, ref CollisionEvent collision) =>
            {
                // 处理碰撞逻辑
                ApplyDamage(collision.EntityA, collision.EntityB);
            })
            .Run();
    }
}
```

#### 2.1.3 Unity ECS碰撞处理的特点

1. **事件驱动**：碰撞信息通过临时的事件Entity和组件传递
2. **自动清理**：事件组件在帧结束时自动清理（或由System手动清理）
3. **系统解耦**：物理系统只负责检测和创建事件，业务逻辑由其他System处理
4. **无回调**：不使用传统的OnCollisionEnter/Stay/Exit回调
5. **数据驱动**：碰撞信息以组件形式存在，可以被多个System查询和处理

#### 2.1.4 与我们的设计对比

| 特性 | Unity Physics | 我们的设计 |
|------|--------------|-----------|
| 碰撞信息存储 | 临时事件Entity + CollisionEvent组件 | CollisionComponent（直接附加到Entity上） |
| 事件清理 | 自动清理（或手动清理） | 每帧开始时清空，然后重新填充 |
| 查询方式 | 查询CollisionEvent组件 | 查询CollisionComponent组件 |
| 设计理念 | 事件驱动（Event-Driven） | 数据驱动（Data-Driven） |

**我们的设计优势：**
- 更简单：不需要创建临时Entity，直接在主Entity上存储碰撞信息
- 更直观：碰撞信息直接关联到Entity，查询更方便
- 更适合帧同步：每帧的碰撞信息都明确存储，便于快照和回滚

## 三、现状分析

### 3.1 现有物理系统架构

**核心类：**
- `RigidBody2D`: 刚体类，包含位置、速度、质量、形状等所有物理属性
- `PhysicsWorld2D`: 物理世界，管理所有刚体，执行物理模拟
- `CollisionShape2D`: 碰撞形状基类
- `BoxShape2D` / `CircleShape2D`: 具体碰撞形状

**问题：**
1. `RigidBody2D`是一个完整的类，包含所有物理属性，不符合ECS的组件化设计
2. `PhysicsWorld2D`直接管理`RigidBody2D`对象列表，而不是通过ECS World管理
3. 物理状态无法直接序列化到ECS的快照系统中
4. 与ECS框架的集成需要额外的适配层

### 3.2 ECS框架特点

**核心设计：**
- `World`: 管理Entity和Component
- `IComponent`: 纯数据结构，实现`ICloneable`接口
- `ISystem`: 系统接口，执行逻辑
- `ECSStateMachine`: 按顺序执行所有System

**优势：**
- 状态快照 = 所有Component的快照
- 回滚 = 恢复所有Component的状态
- 系统解耦，易于扩展

## 四、改造方案

### 4.1 组件设计（Component）

将`RigidBody2D`拆分成多个Component，每个Component只包含相关的数据：

#### 4.1.1 `Transform2DComponent` - 变换组件
```csharp
public struct Transform2DComponent : IComponent
{
    public FixVector2 position;  // 位置（世界坐标）
    
    public object Clone() => new Transform2DComponent { position = position };
}
```

#### 4.1.2 `VelocityComponent` - 速度组件
```csharp
public struct VelocityComponent : IComponent
{
    public FixVector2 velocity;  // 速度（单位/秒）
    
    public object Clone() => new VelocityComponent { velocity = velocity };
}
```

#### 4.1.3 `PhysicsBodyComponent` - 物理体组件（核心）
```csharp
public struct PhysicsBodyComponent : IComponent
{
    // 物理属性
    public Fix64 mass;              // 质量（0或负数表示静态物体）
    public bool isStatic;           // 是否为静态物体
    public bool useGravity;         // 是否受重力影响
    public bool isTrigger;           // 是否为触发器
    
    // 物理参数
    public Fix64 restitution;       // 弹性系数（0-1）
    public Fix64 friction;          // 摩擦系数（0-1）
    public Fix64 linearDamping;     // 线性阻尼（0-1）
    
    // Layer（用于碰撞过滤）
    public int layer;               // 物理层（简化：使用int而不是PhysicsLayer）
    
    // 力累加器（内部使用，不参与序列化）
    // 注意：这个字段在快照时会被忽略，因为每帧都会清除
    
    public object Clone() => new PhysicsBodyComponent
    {
        mass = mass,
        isStatic = isStatic,
        useGravity = useGravity,
        isTrigger = isTrigger,
        restitution = restitution,
        friction = friction,
        linearDamping = linearDamping,
        layer = layer
    };
}
```

#### 4.1.4 `CollisionShapeComponent` - 碰撞形状组件
```csharp
public enum ShapeType : byte
{
    None = 0,
    Circle = 1,
    Box = 2
}

public struct CollisionShapeComponent : IComponent
{
    public ShapeType shapeType;
    
    // 圆形参数
    public Fix64 radius;  // 半径（仅用于Circle）
    
    // 矩形参数
    public FixVector2 size;  // 尺寸（宽度、高度，仅用于Box）
    
    public object Clone() => new CollisionShapeComponent
    {
        shapeType = shapeType,
        radius = radius,
        size = size
    };
    
    // 辅助方法：获取AABB边界
    public FixRect GetBounds(FixVector2 position)
    {
        if (shapeType == ShapeType.Circle)
        {
            return new FixRect(
                position.x - radius,
                position.y - radius,
                radius * Fix64.Two,
                radius * Fix64.Two
            );
        }
        else if (shapeType == ShapeType.Box)
        {
            Fix64 halfWidth = size.x / Fix64.Two;
            Fix64 halfHeight = size.y / Fix64.Two;
            return new FixRect(
                position.x - halfWidth,
                position.y - halfHeight,
                size.x,
                size.y
            );
        }
        return default;
    }
}
```

#### 4.1.5 `CollisionComponent` - 碰撞信息组件（可选）

**设计说明：**
与Unity Physics的事件驱动方式不同，我们采用**数据驱动**的方式：
- Unity Physics：创建临时事件Entity，添加CollisionEvent组件
- 我们的设计：直接在Entity上添加CollisionComponent，存储当前帧的碰撞信息

**优势：**
- 更简单：不需要管理临时Entity的生命周期
- 更直观：碰撞信息直接关联到Entity，查询更方便
- 更适合帧同步：每帧的碰撞信息都明确存储，便于快照和回滚
```csharp
/// <summary>
/// 存储当前帧的碰撞信息
/// 由PhysicsSystem每帧更新，供其他System查询使用
/// 不需要Enter/Stay/Exit机制，只需要知道这一帧和哪些物体相撞了
/// </summary>
public struct CollisionComponent : IComponent
{
    /// <summary>
    /// 当前帧碰撞的Entity ID列表
    /// 每帧开始时由PhysicsSystem清空，然后填充当前帧的碰撞结果
    /// </summary>
    public List<int> collidingEntityIds;
    
    public object Clone() => new CollisionComponent
    {
        collidingEntityIds = collidingEntityIds?.ToList() ?? new List<int>()
    };
}

// 使用示例：
// 在其他System中查询碰撞信息
// if (world.TryGetComponent<CollisionComponent>(entity, out var collision))
// {
//     foreach (var collidingId in collision.collidingEntityIds)
//     {
//         Entity collidingEntity = GetEntityById(world, collidingId);
//         // 处理碰撞逻辑...
//     }
// }
```

### 4.2 系统设计（System）

#### 4.2.1 `PhysicsSystem` - 物理系统（核心）

将`PhysicsWorld2D`的功能改造成`PhysicsSystem`：

```csharp
public class PhysicsSystem : ISystem
{
    // 物理世界配置
    public FixVector2 gravity = FixVector2.Zero;
    public int iterations = 8;  // 碰撞分离迭代次数
    public int subSteps = 2;    // 子步迭代次数
    
    // 碰撞矩阵（Layer -> Layer -> 是否忽略）
    private Dictionary<(int, int), bool> collisionMatrix = new Dictionary<(int, int), bool>();
    
    // 四叉树（用于宽相位碰撞检测）
    private QuadTree quadTree = new QuadTree();
    
    // 子步迭代状态缓存
    private Dictionary<int, (FixVector2 position, FixVector2 velocity)> subStepStateCache;
    
    public void Execute(World world, List<FrameData> inputs)
    {
        // 1. 收集所有有物理体的Entity
        var physicsEntities = CollectPhysicsEntities(world);
        
        if (physicsEntities.Count == 0)
            return;
        
        // 2. 子步迭代
        Fix64 deltaTime = Fix64.One;
        Fix64 subStepDeltaTime = deltaTime / (Fix64)subSteps;
        
        for (int subStep = 0; subStep < subSteps; subStep++)
        {
            if (subStep > 0)
            {
                RestoreState(world, physicsEntities);
            }
            
            UpdateSingleStep(world, physicsEntities, subStepDeltaTime);
            
            if (subStep < subSteps - 1)
            {
                SaveState(world, physicsEntities);
            }
        }
        
        // 3. 碰撞信息已经在ResolveCollisions中记录到CollisionComponent中
        // 其他System可以在后续步骤中查询CollisionComponent来处理碰撞逻辑
    }
    
    private void UpdateSingleStep(World world, List<Entity> entities, Fix64 deltaTime)
    {
        // 1. 收集力（重力等）
        CollectForces(world, entities);
        
        // 2. 更新位置和速度
        UpdatePositions(world, entities, deltaTime);
        
        // 3. 更新四叉树
        UpdateQuadTree(world, entities);
        
        // 4. 碰撞检测和响应（迭代多次）
        for (int i = 0; i < iterations; i++)
        {
            ResolveCollisions(world, entities);
        }
        
        // 5. 清除力累加器
        ClearForces(world, entities);
    }
    
    // 其他辅助方法...
}
```

**关键改造点：**
1. 不再直接管理`RigidBody2D`对象，而是通过ECS World查询Entity
2. 物理属性从Component中读取和写入
3. 力累加器可以存储在临时字典中，或者作为Component的临时字段（不参与序列化）

#### 4.2.2 力累加器的处理

**方案A：使用临时字典（推荐）**
```csharp
// 在PhysicsSystem中维护一个临时字典
private Dictionary<int, FixVector2> forceAccumulator = new Dictionary<int, FixVector2>();

// 每帧开始时清除，每帧结束时清除
// 不参与快照，因为力是瞬时的
```

**方案B：作为Component的临时字段**
```csharp
// 在PhysicsBodyComponent中添加一个字段，但标记为不序列化
// 注意：需要修改快照系统，支持忽略某些字段
```

**推荐使用方案A**，因为：
- 力是瞬时的，不需要保存到快照
- 避免修改快照系统
- 实现更简单

#### 4.2.3 四叉树的管理

四叉树需要存储Entity的引用，但Entity在ECS中是不可变的。解决方案：

**方案A：存储Entity ID**
```csharp
// 修改QuadTree，使其存储Entity ID而不是Entity对象
// 查询时返回Entity ID列表，然后通过World获取Entity
```

**方案B：存储Entity的哈希值**
```csharp
// 使用Entity.GetHashCode()作为键
// 但需要注意Entity的哈希值是否稳定
```

**推荐使用方案A**，因为Entity ID是稳定的。

### 4.3 碰撞检测改造

#### 4.3.1 简化碰撞形状

由于只支持不旋转的矩形和圆形，可以简化：

```csharp
// 在CollisionDetector中，只保留：
// - CircleVsCircle
// - CircleVsBox（不旋转）
// - BoxVsBox（不旋转，即AABB）

// 移除所有旋转相关的代码
```

#### 4.3.2 碰撞检测流程

```csharp
private void ResolveCollisions(World world, List<Entity> entities)
{
    var checkedPairs = new HashSet<(int, int)>();
    
    // 每帧开始时，清空所有Entity的碰撞信息
    ClearCollisionInfo(world, entities);
    
    for (int i = 0; i < entities.Count; i++)
    {
        Entity entityA = entities[i];
        
        // 检查是否有物理体组件
        if (!world.TryGetComponent<PhysicsBodyComponent>(entityA, out var bodyA))
            continue;
        
        // 只处理动态物体
        if (bodyA.isStatic)
            continue;
        
        // 获取位置和形状
        var transformA = world.GetComponent<Transform2DComponent>(entityA);
        var shapeA = world.GetComponent<CollisionShapeComponent>(entityA);
        
        // 宽相位：使用四叉树查询
        FixRect aabbA = shapeA.GetBounds(transformA.position);
        var candidates = quadTree.Query(aabbA);
        
        foreach (var entityBId in candidates)
        {
            // 通过ID获取Entity
            Entity entityB = GetEntityById(world, entityBId);
            if (entityB == default || entityB.Equals(entityA))
                continue;
            
            // 检查碰撞对是否已检测
            var pair = (entityA.Id < entityB.Id) 
                ? (entityA.Id, entityB.Id) 
                : (entityB.Id, entityA.Id);
            if (checkedPairs.Contains(pair))
                continue;
            checkedPairs.Add(pair);
            
            // 检查Layer碰撞矩阵
            var bodyB = world.GetComponent<PhysicsBodyComponent>(entityB);
            if (ShouldIgnoreCollision(bodyA.layer, bodyB.layer))
                continue;
            
            // 窄相位：精确碰撞检测
            var transformB = world.GetComponent<Transform2DComponent>(entityB);
            var shapeB = world.GetComponent<CollisionShapeComponent>(entityB);
            
            if (CheckCollision(shapeA, transformA.position, 
                              shapeB, transformB.position, 
                              out Contact2D contact))
            {
                // 记录碰撞信息（存储到CollisionComponent中）
                RecordCollision(world, entityA, entityB);
                
                // 处理碰撞响应（物理分离和速度修正）
                ResolveContact(world, entityA, entityB, contact);
            }
        }
    }
}

/// <summary>
/// 记录碰撞信息到CollisionComponent
/// 双向记录：entityA和entityB都会记录对方
/// </summary>
private void RecordCollision(World world, Entity entityA, Entity entityB)
{
    // 确保entityA有CollisionComponent
    if (!world.TryGetComponent<CollisionComponent>(entityA, out var collisionA))
    {
        collisionA = new CollisionComponent { collidingEntityIds = new List<int>() };
        world.AddComponent(entityA, collisionA);
    }
    
    // 确保entityB有CollisionComponent
    if (!world.TryGetComponent<CollisionComponent>(entityB, out var collisionB))
    {
        collisionB = new CollisionComponent { collidingEntityIds = new List<int>() };
        world.AddComponent(entityB, collisionB);
    }
    
    // 双向记录（避免重复添加）
    if (!collisionA.collidingEntityIds.Contains(entityB.Id))
    {
        collisionA.collidingEntityIds.Add(entityB.Id);
        world.AddComponent(entityA, collisionA);
    }
    
    if (!collisionB.collidingEntityIds.Contains(entityA.Id))
    {
        collisionB.collidingEntityIds.Add(entityA.Id);
        world.AddComponent(entityB, collisionB);
    }
}

/// <summary>
/// 清空所有Entity的碰撞信息（每帧开始时调用）
/// </summary>
private void ClearCollisionInfo(World world, List<Entity> entities)
{
    foreach (var entity in entities)
    {
        if (world.TryGetComponent<CollisionComponent>(entity, out var collision))
        {
            collision.collidingEntityIds.Clear();
            world.AddComponent(entity, collision);
        }
    }
}
```

**碰撞信息的使用方式：**

其他System可以查询碰撞信息，手动处理碰撞逻辑：

```csharp
// 在其他System中（如DamageSystem、PickupSystem等）
public class DamageSystem : ISystem
{
    public void Execute(World world, List<FrameData> inputs)
    {
        // 查询所有有碰撞信息的Entity
        foreach (var entity in world.GetEntitiesWithComponent<CollisionComponent>())
        {
            var collision = world.GetComponent<CollisionComponent>(entity);
            
            // 遍历所有碰撞的Entity
            foreach (var collidingId in collision.collidingEntityIds)
            {
                Entity collidingEntity = GetEntityById(world, collidingId);
                
                // 根据业务逻辑处理碰撞
                // 例如：检查是否是敌人，造成伤害等
                if (IsEnemy(collidingEntity))
                {
                    ApplyDamage(entity, collidingEntity);
                }
            }
        }
    }
}
```

### 4.4 与ECS框架集成

#### 4.4.1 注册物理系统

在`ECSStateMachine.InitializeDefaultSystems()`中注册：

```csharp
public static void InitializeDefaultSystems()
{
    ClearSystems();
    
    // 注册物理系统（在其他系统之前执行，因为物理会影响位置）
    RegisterSystem(new PhysicsSystem());
    
    // 其他系统...
    RegisterSystem(new PlayerMoveSystem());
    RegisterSystem(new PlayerShootSystem());
    RegisterSystem(new BulletMoveSystem());
    
    _initialized = true;
}
```

#### 4.4.2 创建物理Entity

```csharp
// 创建一个有物理的Entity
Entity physicsEntity = world.CreateEntity();

// 添加变换组件
world.AddComponent(physicsEntity, new Transform2DComponent 
{ 
    position = new FixVector2(0, 0) 
});

// 添加速度组件
world.AddComponent(physicsEntity, new VelocityComponent 
{ 
    velocity = FixVector2.Zero 
});

// 添加物理体组件
world.AddComponent(physicsEntity, new PhysicsBodyComponent
{
    mass = (Fix64)1.0f,
    isStatic = false,
    useGravity = true,
    restitution = (Fix64)0.5f,
    friction = (Fix64)0.5f,
    linearDamping = Fix64.Zero,
    layer = 1
});

// 添加碰撞形状组件
world.AddComponent(physicsEntity, new CollisionShapeComponent
{
    shapeType = ShapeType.Circle,
    radius = (Fix64)0.5f
});
```

### 4.5 快照和回滚支持

由于所有物理状态都存储在Component中，快照和回滚会自动支持：

1. **快照**：`ECSGameState.CreateSnapshot()`会自动保存所有Component，包括物理相关的Component
2. **回滚**：`ECSGameState.RestoreToWorld()`会自动恢复所有Component，包括物理相关的Component

**注意事项：**
- **力累加器**：不参与快照（使用临时字典存储，每帧都会清除）
- **碰撞信息（CollisionComponent）**：参与快照
  - 虽然碰撞信息是每帧的瞬时数据，但需要包含在快照中
  - 原因：其他System（如DamageSystem）可能在查询历史帧的碰撞信息
  - 回滚时，碰撞信息会被恢复，确保其他System能正确查询
  - 每帧开始时，PhysicsSystem会清空碰撞信息，然后重新填充

### 4.6 迁移策略

#### 4.6.1 保留现有代码

在改造过程中，保留现有的`PhysicsWorld2D`和`RigidBody2D`代码，作为参考和对比。

#### 4.6.2 逐步迁移

1. **第一阶段**：创建新的Component和System
2. **第二阶段**：实现基本的物理模拟（重力、速度、位置更新）
3. **第三阶段**：实现碰撞检测（简化版：只支持不旋转的矩形和圆形）
4. **第四阶段**：集成到ECSStateMachine
5. **第五阶段**：测试和优化

#### 4.6.3 兼容性处理

如果需要同时支持新旧两套物理系统，可以：
- 使用命名空间区分：`Frame.Physics2D.ECS` vs `Frame.Physics2D.Legacy`
- 或者使用条件编译：`#if ECS_PHYSICS`

## 五、实现细节

### 5.1 文件结构

```
RollPredict/Assets/Scripts/ECS/
├── Components/
│   ├── Transform2DComponent.cs          # 新增
│   ├── VelocityComponent.cs             # 新增
│   ├── PhysicsBodyComponent.cs           # 新增
│   ├── CollisionShapeComponent.cs        # 新增
│   └── CollisionComponent.cs            # 新增（可选，用于存储碰撞信息）
├── System/
│   ├── PhysicsSystem.cs                 # 新增
│   └── ...
└── ...

RollPredict/Assets/3rd/Physics/Physics2D/
├── ECS/                                   # 新增目录
│   ├── PhysicsSystem.cs                  # 物理系统
│   ├── CollisionDetectorECS.cs           # 碰撞检测器（简化版）
│   └── ...
└── ...                                    # 保留原有代码
```

### 5.2 关键实现点

#### 5.2.1 Entity ID的获取

ECS框架中的Entity需要提供ID访问：

```csharp
// 在Entity类中添加Id属性
public class Entity
{
    public int Id { get; private set; }
    // ...
}
```

#### 5.2.2 四叉树的改造

```csharp
// 修改QuadTree，使其存储Entity ID
public class QuadTree
{
    private List<int> objects = new List<int>();  // 存储Entity ID
    
    public void AddObject(int entityId) { ... }
    public void RemoveObject(int entityId) { ... }
    public void UpdateObject(int entityId) { ... }
    public List<int> Query(FixRect bounds) { ... }  // 返回Entity ID列表
}
```

#### 5.2.3 碰撞检测的简化

```csharp
// 只实现不旋转的碰撞检测
public static class CollisionDetectorECS
{
    // Circle vs Circle（保持不变）
    public static bool CircleVsCircle(...) { ... }
    
    // Circle vs Box（不旋转，使用AABB）
    public static bool CircleVsBoxAABB(...) { ... }
    
    // Box vs Box（不旋转，使用AABB）
    public static bool BoxVsBoxAABB(...) { ... }
}
```

## 六、优势分析

### 6.1 与ECS框架的集成

1. **状态快照**：物理状态自动支持快照和回滚
2. **系统解耦**：物理系统与其他系统解耦，易于扩展
3. **数据驱动**：物理属性通过Component配置，易于调整

### 6.2 性能优化

1. **组件化**：只查询需要的Component，减少内存占用
2. **确定性**：使用定点数，保证帧同步的确定性
3. **可扩展**：易于添加新的物理特性（如角速度、旋转等）

### 6.3 代码维护

1. **清晰的结构**：Component只包含数据，System只包含逻辑
2. **易于测试**：可以单独测试每个System
3. **易于扩展**：添加新的物理特性只需要添加新的Component和System

## 七、注意事项

### 7.1 确定性保证

1. **遍历顺序**：确保Entity的遍历顺序是确定的（ECS框架已使用OrderedHashSet）
2. **浮点数**：所有计算使用定点数（Fix64）
3. **随机数**：使用确定性随机数生成器

### 7.2 性能考虑

1. **四叉树优化**：继续使用四叉树进行宽相位碰撞检测
2. **组件查询**：使用`GetEntitiesWithComponent<T>()`批量查询，避免逐个遍历
3. **内存分配**：尽量减少临时对象的分配（使用对象池）

### 7.3 兼容性

1. **向后兼容**：保留原有物理系统代码，确保现有功能不受影响
2. **渐进迁移**：可以逐步将游戏对象迁移到新的物理系统

## 八、总结

通过将物理系统改造成ECS架构，我们可以：

1. **无缝集成**：物理系统与ECS框架完美集成，支持预测回滚
2. **简化实现**：只支持不旋转的矩形和圆形，降低复杂度
3. **易于扩展**：组件化设计，易于添加新功能
4. **保持性能**：继续使用四叉树等优化技术
5. **简化的碰撞处理**：
   - 不需要Enter/Stay/Exit回调机制
   - 只需要知道当前帧和哪些物体相撞了
   - 通过`CollisionComponent`存储碰撞信息，其他System手动查询和处理
   - 符合ECS的数据驱动设计理念

改造的核心思想是：**将物理属性拆分成多个Component，将物理逻辑封装成System，通过ECS World统一管理**。

**碰撞处理的设计理念：**
- PhysicsSystem负责物理模拟和碰撞检测，将碰撞信息记录到`CollisionComponent`中
- 其他System（如DamageSystem、PickupSystem等）通过查询`CollisionComponent`来获取碰撞信息
- 每个System根据自己的业务逻辑手动处理碰撞，而不是通过回调机制
- 这种方式更加灵活，符合ECS的解耦设计

