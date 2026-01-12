# ECS碰撞信息存储方案

## 一、问题分析

### 1.1 当前问题

在`CollisionComponent`结构体中直接使用`List<int>`存在以下问题：

1. **值语义破坏**：结构体应该是值类型，但`List<>`是引用类型，导致结构体的值语义被破坏
2. **深拷贝问题**：`Clone()`方法需要手动深拷贝List，容易出错
3. **性能问题**：结构体赋值时会复制引用，可能导致意外的共享状态
4. **内存管理**：List的内存分配在堆上，增加GC压力
5. **确定性**：在帧同步中，引用类型可能导致不确定性问题

### 1.2 代码示例

```csharp
public struct CollisionComponent : IComponent
{
    public List<int> collidingEntityIds;  // ❌ 问题：结构体中包含引用类型
}
```

**问题场景：**
```csharp
var collision1 = new CollisionComponent();
collision1.collidingEntityIds = new List<int> { 1, 2, 3 };

var collision2 = collision1;  // 浅拷贝，两个结构体共享同一个List引用
collision2.collidingEntityIds.Add(4);  // 这会影响collision1！

// 在快照/回滚时，这会导致严重问题
```

## 二、解决方案对比

### 方案A：使用固定大小数组（推荐用于简单场景）

**优点：**
- 值类型，完全符合结构体的语义
- 无GC压力
- 确定性好
- 实现简单

**缺点：**
- 需要限制最大碰撞数量
- 需要手动管理数组大小

**实现：**
```csharp
public struct CollisionComponent : IComponent
{
    private const int MaxCollisions = 8;  // 最大碰撞数量
    
    private int _count;  // 当前碰撞数量
    private int _collision0, _collision1, _collision2, _collision3;
    private int _collision4, _collision5, _collision6, _collision7;
    
    public int Count => _count;
    
    public void AddCollidingEntity(int entityId)
    {
        if (_count >= MaxCollisions) return;
        
        // 检查是否已存在
        for (int i = 0; i < _count; i++)
        {
            if (GetCollision(i) == entityId) return;
        }
        
        // 添加到数组
        SetCollision(_count, entityId);
        _count++;
    }
    
    public int GetCollision(int index)
    {
        return index switch
        {
            0 => _collision0,
            1 => _collision1,
            2 => _collision2,
            3 => _collision3,
            4 => _collision4,
            5 => _collision5,
            6 => _collision6,
            7 => _collision7,
            _ => 0
        };
    }
    
    private void SetCollision(int index, int value)
    {
        switch (index)
        {
            case 0: _collision0 = value; break;
            case 1: _collision1 = value; break;
            case 2: _collision2 = value; break;
            case 3: _collision3 = value; break;
            case 4: _collision4 = value; break;
            case 5: _collision5 = value; break;
            case 6: _collision6 = value; break;
            case 7: _collision7 = value; break;
        }
    }
    
    public void Clear()
    {
        _count = 0;
    }
    
    public object Clone()
    {
        return this;  // 结构体直接拷贝即可
    }
}
```

**使用方式：**
```csharp
// 方式1：遍历碰撞（推荐）
if (world.TryGetComponent<CollisionComponent>(entity, out var collision))
{
    for (int i = 0; i < collision.Count; i++)
    {
        int collidingId = collision.GetCollision(i);
        Entity collidingEntity = GetEntityById(world, collidingId);
        // 处理碰撞...
    }
}

// 方式2：检查是否包含特定Entity
if (collision.Contains(targetEntityId))
{
    // 处理碰撞...
}

// 方式3：获取所有碰撞列表（如果需要）
List<int> allCollisions = collision.GetAllCollisions();
```

### 方案B：使用外部字典存储（推荐用于复杂场景）

**优点：**
- 不限制碰撞数量
- 结构体保持值语义
- 性能好（字典查找O(1)）
- 易于扩展

**缺点：**
- 需要额外的存储系统
- 需要手动管理生命周期

**实现：**
```csharp
// CollisionComponent只存储引用（或使用位掩码）
public struct CollisionComponent : IComponent
{
    // 方案B1：使用位掩码（如果Entity ID范围较小）
    // public ulong collisionMask;  // 最多64个Entity
    
    // 方案B2：使用外部字典的键（Entity ID作为键）
    // 不需要存储任何数据，通过Entity ID在外部字典中查找
    // 这个Component只作为标记，表示该Entity有碰撞信息
    
    public object Clone()
    {
        return this;  // 空结构体，直接拷贝
    }
}

// 在PhysicsSystem中维护外部字典
public class PhysicsSystem : ISystem
{
    // Entity ID -> 碰撞的Entity ID列表
    private Dictionary<int, List<int>> collisionData = new Dictionary<int, List<int>>();
    
    private void RecordCollision(World world, Entity entityA, Entity entityB)
    {
        // 确保有CollisionComponent标记
        if (!world.HasComponent<CollisionComponent>(entityA))
            world.AddComponent(entityA, new CollisionComponent());
        if (!world.HasComponent<CollisionComponent>(entityB))
            world.AddComponent(entityB, new CollisionComponent());
        
        // 在外部字典中存储碰撞信息
        if (!collisionData.ContainsKey(entityA.Id))
            collisionData[entityA.Id] = new List<int>();
        if (!collisionData.ContainsKey(entityB.Id))
            collisionData[entityB.Id] = new List<int>();
        
        if (!collisionData[entityA.Id].Contains(entityB.Id))
            collisionData[entityA.Id].Add(entityB.Id);
        if (!collisionData[entityB.Id].Contains(entityA.Id))
            collisionData[entityB.Id].Add(entityA.Id);
    }
    
    // 查询碰撞信息
    public List<int> GetCollidingEntities(int entityId)
    {
        return collisionData.TryGetValue(entityId, out var list) 
            ? new List<int>(list)  // 返回副本
            : new List<int>();
    }
    
    // 每帧开始时清空
    private void ClearCollisionInfo()
    {
        collisionData.Clear();
    }
}
```

### 方案C：使用固定大小数组 + 溢出处理

**优点：**
- 大部分情况下使用固定数组（性能好）
- 超出限制时使用外部字典（灵活性好）

**缺点：**
- 实现较复杂
- 需要两套存储机制

**实现：**
```csharp
public struct CollisionComponent : IComponent
{
    private const int MaxCollisions = 4;  // 固定数组大小
    
    private int _count;
    private int _collision0, _collision1, _collision2, _collision3;
    
    // 如果超过MaxCollisions，使用外部字典
    // 通过_count > MaxCollisions来判断是否溢出
    
    // ... 类似方案A的实现
}

// 在PhysicsSystem中
private Dictionary<int, List<int>> overflowCollisions = new Dictionary<int, List<int>>();
```

### 方案D：将CollisionComponent改为class

**优点：**
- 实现最简单
- 不限制碰撞数量

**缺点：**
- 破坏ECS的值语义设计
- 需要手动深拷贝
- GC压力大
- 不符合ECS设计理念

**不推荐使用此方案**

## 三、推荐方案

### 3.1 简单场景：方案A（固定大小数组）

**适用场景：**
- 单个Entity的碰撞数量通常不超过8个
- 对性能要求高
- 需要确定性（帧同步）

**实现建议：**
- 使用固定大小数组（4-8个元素）
- 如果超出限制，记录警告或忽略

### 3.2 复杂场景：方案B（外部字典）

**适用场景：**
- 碰撞数量不确定
- 需要灵活性
- 可以接受额外的存储系统

**实现建议：**
- CollisionComponent只作为标记
- 在PhysicsSystem中维护Dictionary<int, List<int>>
- 提供查询接口

## 四、最终推荐实现

### 推荐：方案A（固定大小数组，优化版）

使用更优雅的实现方式：

```csharp
public struct CollisionComponent : IComponent
{
    private const int MaxCollisions = 8;
    
    private int _count;
    private unsafe fixed int _collisions[MaxCollisions];  // 固定数组
    
    // 或者使用更简单的方式：直接使用多个字段
    private int _c0, _c1, _c2, _c3, _c4, _c5, _c6, _c7;
    
    public int Count => _count;
    
    public void AddCollidingEntity(int entityId)
    {
        // 检查是否已存在
        for (int i = 0; i < _count; i++)
        {
            if (GetCollision(i) == entityId) return;
        }
        
        if (_count >= MaxCollisions)
        {
            // 超出限制，可以选择：
            // 1. 忽略（简单）
            // 2. 记录警告
            // 3. 使用外部字典（复杂）
            return;
        }
        
        SetCollision(_count++, entityId);
    }
    
    public int GetCollision(int index)
    {
        if (index < 0 || index >= _count) return 0;
        
        return index switch
        {
            0 => _c0, 1 => _c1, 2 => _c2, 3 => _c3,
            4 => _c4, 5 => _c5, 6 => _c6, 7 => _c7,
            _ => 0
        };
    }
    
    private void SetCollision(int index, int value)
    {
        switch (index)
        {
            case 0: _c0 = value; break;
            case 1: _c1 = value; break;
            case 2: _c2 = value; break;
            case 3: _c3 = value; break;
            case 4: _c4 = value; break;
            case 5: _c5 = value; break;
            case 6: _c6 = value; break;
            case 7: _c7 = value; break;
        }
    }
    
    public void Clear()
    {
        _count = 0;
    }
    
    public object Clone()
    {
        return this;  // 结构体直接拷贝
    }
    
    // 提供迭代器支持
    public IEnumerable<int> GetCollisions()
    {
        for (int i = 0; i < _count; i++)
        {
            yield return GetCollision(i);
        }
    }
}
```

## 五、性能对比

| 方案 | 内存分配 | GC压力 | 查询性能 | 确定性 | 实现复杂度 |
|------|---------|--------|---------|--------|-----------|
| 方案A（固定数组） | 栈/值类型 | 无 | O(n) | 高 | 低 |
| 方案B（外部字典） | 堆 | 中等 | O(1) | 中等 | 中 |
| 方案C（混合） | 混合 | 低 | O(1)/O(n) | 高 | 高 |
| 方案D（class） | 堆 | 高 | O(n) | 低 | 低 |

## 六、最终实现（方案A：固定大小数组）

### 6.0 为什么需要回滚时应该用Component？

**关键问题：碰撞信息需要参与快照和回滚**

1. **System中的字典是临时的**：
   - System中的`Dictionary<int, List<int>>`是实例变量，不会参与快照
   - 回滚时，System的状态不会被恢复
   - 如果碰撞信息只存在System中，回滚后无法恢复碰撞状态

2. **Component会自动参与快照**：
   - ECS框架的快照系统会自动保存所有Component
   - 回滚时会自动恢复所有Component
   - 碰撞信息存储在Component中，可以正确回滚

3. **碰撞信息是游戏状态的一部分**：
   - 其他System可能依赖碰撞信息（如DamageSystem）
   - 回滚时需要恢复完整的游戏状态，包括碰撞信息

**结论：碰撞信息必须存储在Component中，不能只存在System中。**

## 七、最终实现（方案A：固定大小数组）

### 7.1 实现说明

**已采用方案A（固定大小数组）**，原因：

1. **需要参与快照和回滚**：碰撞信息是游戏状态的一部分，必须存储在Component中
2. **符合ECS设计**：结构体保持值语义，支持直接拷贝
3. **性能优秀**：无GC压力，栈分配
4. **确定性好**：适合帧同步
5. **实用性强**：大多数游戏场景中，单个Entity的碰撞数量不会超过8个

### 7.2 实现细节

**CollisionComponent实现：**
```csharp
public struct CollisionComponent : IComponent
{
    private const int MaxCollisions = 8;  // 最大碰撞数量
    private int _count;
    private int _c0, _c1, _c2, _c3, _c4, _c5, _c6, _c7;  // 固定数组
    
    public void AddCollidingEntity(int entityId) { ... }
    public int GetCollision(int index) { ... }
    public void Clear() { ... }
    public bool Contains(int entityId) { ... }
    
    public object Clone()
    {
        return this;  // 结构体直接拷贝即可
    }
}
```

**PhysicsSystem中的使用：**
```csharp
private void RecordCollision(World world, Entity entityA, Entity entityB)
{
    // 确保有CollisionComponent
    if (!world.TryGetComponent<CollisionComponent>(entityA, out var collisionA))
    {
        collisionA = new CollisionComponent();
        world.AddComponent(entityA, collisionA);
    }
    
    // 双向记录
    collisionA.AddCollidingEntity(entityB.Id);
    collisionB.AddCollidingEntity(entityA.Id);
    
    world.AddComponent(entityA, collisionA);
    world.AddComponent(entityB, collisionB);
}
```

### 7.3 使用方式

**在其他System中查询碰撞信息：**

```csharp
public class DamageSystem : ISystem
{
    public void Execute(World world, List<FrameData> inputs)
    {
        // 查询所有有碰撞信息的Entity
        foreach (var entity in world.GetEntitiesWithComponent<CollisionComponent>())
        {
            var collision = world.GetComponent<CollisionComponent>(entity);
            
            // 遍历所有碰撞的Entity
            for (int i = 0; i < collision.Count; i++)
            {
                int collidingId = collision.GetCollision(i);
                Entity collidingEntity = GetEntityById(world, collidingId);
                
                // 根据业务逻辑处理碰撞
                if (IsEnemy(collidingEntity))
                {
                    ApplyDamage(entity, collidingEntity);
                }
            }
            
            // 或者检查是否包含特定Entity
            if (collision.Contains(targetEntityId))
            {
                // 处理碰撞...
            }
        }
    }
}
```

### 7.4 快照和回滚支持

**自动支持：**
- `ECSGameState.CreateSnapshot()`会自动保存所有Component，包括`CollisionComponent`
- `ECSGameState.RestoreToWorld()`会自动恢复所有Component，包括`CollisionComponent`
- 回滚时，碰撞信息会被正确恢复

**注意事项：**
- 碰撞信息参与快照，因为它是游戏状态的一部分
- 每帧开始时，PhysicsSystem会清空碰撞信息，然后重新填充
- 回滚后，碰撞信息会恢复到快照时的状态

### 7.5 限制说明

**最大碰撞数量限制：**
- 当前限制为8个碰撞
- 如果超出限制，会忽略额外的碰撞（可以记录警告）
- 如果需要更多，可以增加数组大小（如16个）

**适用场景：**
- 大多数游戏场景中，单个Entity的碰撞数量不会超过8个
- 如果确实需要更多，可以考虑：
  1. 增加数组大小（如16个）
  2. 或者使用混合方案（固定数组 + 外部字典处理溢出）

## 八、总结

**已实现方案A（固定大小数组）**，优势：

1. **参与快照和回滚**：碰撞信息存储在Component中，自动参与快照和回滚
2. **符合ECS设计**：结构体保持值语义，支持直接拷贝
3. **性能优秀**：无GC压力，栈分配
4. **确定性好**：适合帧同步
5. **实现简单**：代码清晰，易于维护

**关键决策：**
- **为什么必须用Component？** 因为碰撞信息需要参与快照和回滚，System中的字典不会参与快照
- **为什么用固定数组而不是List？** 因为结构体中不能使用引用类型，固定数组是值类型
- **限制是否可接受？** 大多数游戏场景中，单个Entity的碰撞数量不会超过8个，如果需要更多可以增加数组大小

**适用场景：**
- 需要快照和回滚的帧同步游戏
- 单个Entity的碰撞数量通常不超过8个
- 对性能要求高，需要确定性

