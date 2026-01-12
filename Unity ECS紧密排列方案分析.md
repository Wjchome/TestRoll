# Unity ECS 紧密排列方案分析

## 一、问题分析

### 需求
- ✅ 使用 Unity ECS 的 `IComponentData`（紧密排列的结构体）
- ❌ 不使用 Unity 的 `Entity` 和 `System`
- ✅ 自己管理数据存储，但利用紧密排列特性

## 二、Unity ECS 紧密排列的原理

### 2.1 Unity ECS 的数据布局
Unity ECS 通过以下机制实现紧密排列：

1. **Chunk 系统**：相同组件组合的 Entity 存储在同一个 Chunk（16KB）
2. **SoA（Structure of Arrays）**：每个组件类型一个数组
3. **NativeArray**：使用 Unity 的 Native Collections 实现

### 2.2 关键点
- **紧密排列依赖于 EntityManager**：`EntityManager` 负责管理 Chunk 和组件存储
- **不能单独使用 IComponentData**：`IComponentData` 只是一个标记接口，不提供存储

## 三、可行方案

### 方案1：使用 NativeArray 自己实现紧密排列 ✅ 推荐

**核心思想**：不使用 Unity ECS 的 Entity/System，但使用 `NativeArray` 实现紧密排列。

#### 实现示例

```csharp
using Unity.Collections;
using Unity.Entities;

// 1. 定义组件（使用 IComponentData 标记，但不依赖 Unity ECS）
public struct Transform2DComponent : IComponentData
{
    public FixVector2 position;
}

public struct VelocityComponent : IComponentData
{
    public FixVector2 velocity;
}

// 2. 自己实现紧密排列的存储
public class DenseComponentStorage<T> where T : struct, IComponentData
{
    // 使用 NativeList 实现紧密排列（类似数组）
    private NativeList<T> _components;
    
    // Entity ID 到数组索引的映射
    private NativeHashMap<int, int> _entityToIndex;
    
    // 数组索引到 Entity ID 的映射（用于反向查找）
    private NativeList<int> _indexToEntity;
    
    public DenseComponentStorage(int initialCapacity, Allocator allocator)
    {
        _components = new NativeList<T>(initialCapacity, allocator);
        _entityToIndex = new NativeHashMap<int, int>(initialCapacity, allocator);
        _indexToEntity = new NativeList<int>(initialCapacity, allocator);
    }
    
    // 添加组件（紧密排列）
    public void Add(int entityId, T component)
    {
        if (_entityToIndex.ContainsKey(entityId))
        {
            // 更新现有组件
            int index = _entityToIndex[entityId];
            _components[index] = component;
        }
        else
        {
            // 添加新组件（紧密排列）
            int index = _components.Length;
            _components.Add(component);
            _entityToIndex[entityId] = index;
            _indexToEntity.Add(entityId);
        }
    }
    
    // 获取组件（通过 Entity ID）
    public bool TryGet(int entityId, out T component)
    {
        if (_entityToIndex.TryGetValue(entityId, out int index))
        {
            component = _components[index];
            return true;
        }
        component = default;
        return false;
    }
    
    // 移除组件（保持紧密排列）
    public bool Remove(int entityId)
    {
        if (!_entityToIndex.TryGetValue(entityId, out int index))
            return false;
        
        // 使用"交换并删除"技术保持紧密排列
        int lastIndex = _components.Length - 1;
        if (index != lastIndex)
        {
            // 将最后一个元素移到当前位置
            _components[index] = _components[lastIndex];
            int lastEntityId = _indexToEntity[lastIndex];
            _entityToIndex[lastEntityId] = index;
            _indexToEntity[index] = lastEntityId;
        }
        
        // 删除最后一个元素
        _components.RemoveAtSwapBack(lastIndex);
        _indexToEntity.RemoveAtSwapBack(lastIndex);
        _entityToIndex.Remove(entityId);
        
        return true;
    }
    
    // 获取所有组件（紧密排列的数组）
    public NativeArray<T> GetAllComponents(Allocator allocator)
    {
        return new NativeArray<T>(_components.AsArray(), allocator);
    }
    
    // 遍历所有组件（高效，因为紧密排列）
    public void ForEach(System.Action<int, T> action)
    {
        for (int i = 0; i < _components.Length; i++)
        {
            int entityId = _indexToEntity[i];
            action(entityId, _components[i]);
        }
    }
    
    // 清理
    public void Dispose()
    {
        if (_components.IsCreated) _components.Dispose();
        if (_entityToIndex.IsCreated) _entityToIndex.Dispose();
        if (_indexToEntity.IsCreated) _indexToEntity.Dispose();
    }
}
```

#### 使用示例

```csharp
public class World
{
    private DenseComponentStorage<Transform2DComponent> _transforms;
    private DenseComponentStorage<VelocityComponent> _velocities;
    
    public World()
    {
        _transforms = new DenseComponentStorage<Transform2DComponent>(100, Allocator.Persistent);
        _velocities = new DenseComponentStorage<VelocityComponent>(100, Allocator.Persistent);
    }
    
    public void AddComponent<T>(int entityId, T component) where T : struct, IComponentData
    {
        // 根据类型分发到不同的存储
        if (component is Transform2DComponent transform)
            _transforms.Add(entityId, transform);
        else if (component is VelocityComponent velocity)
            _velocities.Add(entityId, velocity);
    }
    
    // 批量处理（利用紧密排列的优势）
    public void UpdatePositions(float deltaTime)
    {
        // 紧密排列，CPU 缓存友好
        _transforms.ForEach((entityId, transform) =>
        {
            if (_velocities.TryGet(entityId, out var velocity))
            {
                transform.position += velocity.velocity * deltaTime;
                _transforms.Add(entityId, transform);
            }
        });
    }
}
```

### 方案2：使用 Unity.Collections 的 NativeArray/NativeList ✅ 可行

**更简单的方案**：直接使用 `NativeArray` 或 `NativeList`，不依赖 Unity ECS。

```csharp
using Unity.Collections;

public class SimpleDenseStorage<T> where T : struct
{
    private NativeList<T> _data;
    private Dictionary<int, int> _entityToIndex;  // Entity ID -> 数组索引
    private List<int> _indexToEntity;              // 数组索引 -> Entity ID
    
    public SimpleDenseStorage(int capacity)
    {
        _data = new NativeList<T>(capacity, Allocator.Persistent);
        _entityToIndex = new Dictionary<int, int>();
        _indexToEntity = new List<int>();
    }
    
    // 添加/更新组件
    public void Set(int entityId, T component)
    {
        if (_entityToIndex.TryGetValue(entityId, out int index))
        {
            _data[index] = component;  // 更新
        }
        else
        {
            // 添加（紧密排列）
            int newIndex = _data.Length;
            _data.Add(component);
            _entityToIndex[entityId] = newIndex;
            _indexToEntity.Add(entityId);
        }
    }
    
    // 获取组件
    public bool TryGet(int entityId, out T component)
    {
        if (_entityToIndex.TryGetValue(entityId, out int index))
        {
            component = _data[index];
            return true;
        }
        component = default;
        return false;
    }
    
    // 获取紧密排列的数组（用于批量处理）
    public NativeArray<T> GetDenseArray(Allocator allocator)
    {
        return new NativeArray<T>(_data.AsArray(), allocator);
    }
    
    public void Dispose()
    {
        if (_data.IsCreated)
            _data.Dispose();
    }
}
```

### 方案3：混合方案（部分使用 Unity ECS）⚠️ 不推荐

**使用 EntityManager 但不使用 System**：

```csharp
// 创建 EntityManager
EntityManager entityManager = new EntityManager();

// 创建 Entity（但不使用 Unity 的 System）
Entity entity = entityManager.CreateEntity();

// 添加组件（紧密排列）
entityManager.AddComponentData(entity, new Transform2DComponent { position = ... });

// 查询组件（紧密排列）
var query = entityManager.CreateEntityQuery(typeof(Transform2DComponent));
var transforms = query.ToComponentDataArray<Transform2DComponent>(Allocator.TempJob);
// transforms 是紧密排列的数组
```

**问题**：
- 仍然依赖 `EntityManager` 和 `Entity`
- 需要管理 `World` 和 `EntityManager` 的生命周期
- 复杂度较高

## 四、方案对比

| 方案 | 紧密排列 | 独立性 | 复杂度 | 性能 | 推荐度 |
|------|---------|--------|--------|------|--------|
| **方案1：NativeArray + 映射** | ✅ | ✅ | 中 | 高 | ⭐⭐⭐⭐⭐ |
| **方案2：NativeList + Dictionary** | ✅ | ✅ | 低 | 中 | ⭐⭐⭐⭐ |
| **方案3：部分使用 Unity ECS** | ✅ | ❌ | 高 | 高 | ⭐⭐ |

## 五、推荐实现（方案1 完整版）

```csharp
using Unity.Collections;
using Unity.Entities;
using System.Collections.Generic;

namespace Frame.ECS
{
    /// <summary>
    /// 紧密排列的组件存储（不使用 Unity ECS 的 Entity/System）
    /// 使用 NativeArray 实现紧密排列，提高 CPU 缓存命中率
    /// </summary>
    public class DenseComponentStorage<T> : IDisposable where T : struct, IComponentData
    {
        // 紧密排列的组件数组
        private NativeList<T> _components;
        
        // Entity ID -> 数组索引映射
        private NativeHashMap<int, int> _entityToIndex;
        
        // 数组索引 -> Entity ID 映射（用于遍历）
        private NativeList<int> _indexToEntity;
        
        // 是否已释放
        private bool _disposed = false;
        
        public DenseComponentStorage(int initialCapacity = 100)
        {
            _components = new NativeList<T>(initialCapacity, Allocator.Persistent);
            _entityToIndex = new NativeHashMap<int, int>(initialCapacity, Allocator.Persistent);
            _indexToEntity = new NativeList<int>(initialCapacity, Allocator.Persistent);
        }
        
        /// <summary>
        /// 添加或更新组件（紧密排列）
        /// </summary>
        public void Set(int entityId, T component)
        {
            if (_entityToIndex.TryGetValue(entityId, out int index))
            {
                // 更新现有组件（原地更新，保持紧密排列）
                _components[index] = component;
            }
            else
            {
                // 添加新组件（追加到数组末尾，保持紧密排列）
                int newIndex = _components.Length;
                _components.Add(component);
                _entityToIndex[entityId] = newIndex;
                _indexToEntity.Add(entityId);
            }
        }
        
        /// <summary>
        /// 获取组件
        /// </summary>
        public bool TryGet(int entityId, out T component)
        {
            if (_entityToIndex.TryGetValue(entityId, out int index))
            {
                component = _components[index];
                return true;
            }
            component = default;
            return false;
        }
        
        /// <summary>
        /// 移除组件（使用"交换并删除"保持紧密排列）
        /// </summary>
        public bool Remove(int entityId)
        {
            if (!_entityToIndex.TryGetValue(entityId, out int index))
                return false;
            
            int lastIndex = _components.Length - 1;
            
            if (index != lastIndex)
            {
                // 将最后一个元素移到当前位置（保持紧密排列）
                _components[index] = _components[lastIndex];
                
                int lastEntityId = _indexToEntity[lastIndex];
                _entityToIndex[lastEntityId] = index;
                _indexToEntity[index] = lastEntityId;
            }
            
            // 删除最后一个元素
            _components.RemoveAtSwapBack(lastIndex);
            _indexToEntity.RemoveAtSwapBack(lastIndex);
            _entityToIndex.Remove(entityId);
            
            return true;
        }
        
        /// <summary>
        /// 获取紧密排列的数组（用于批量处理）
        /// </summary>
        public NativeArray<T> GetDenseArray(Allocator allocator)
        {
            return new NativeArray<T>(_components.AsArray(), allocator);
        }
        
        /// <summary>
        /// 遍历所有组件（高效，因为紧密排列）
        /// </summary>
        public void ForEach(System.Action<int, T> action)
        {
            for (int i = 0; i < _components.Length; i++)
            {
                int entityId = _indexToEntity[i];
                action(entityId, _components[i]);
            }
        }
        
        /// <summary>
        /// 获取组件数量
        /// </summary>
        public int Count => _components.Length;
        
        /// <summary>
        /// 检查 Entity 是否有此组件
        /// </summary>
        public bool Has(int entityId)
        {
            return _entityToIndex.ContainsKey(entityId);
        }
        
        /// <summary>
        /// 获取所有 Entity ID
        /// </summary>
        public NativeArray<int> GetAllEntityIds(Allocator allocator)
        {
            return new NativeArray<int>(_indexToEntity.AsArray(), allocator);
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            
            if (_components.IsCreated) _components.Dispose();
            if (_entityToIndex.IsCreated) _entityToIndex.Dispose();
            if (_indexToEntity.IsCreated) _indexToEntity.Dispose();
            
            _disposed = true;
        }
    }
}
```

## 六、优势分析

### 6.1 紧密排列的优势
1. **CPU 缓存友好**：数据连续存储，减少缓存未命中
2. **批量处理高效**：可以一次性处理整个数组
3. **内存局部性好**：访问模式更符合 CPU 预取

### 6.2 保持独立性的优势
1. **不依赖 Unity ECS**：可以独立使用
2. **保持当前架构**：不需要重写 Entity/System
3. **易于集成**：可以逐步替换现有的 ComponentStorage

## 七、注意事项

### 7.1 内存管理
- `NativeList` 和 `NativeHashMap` 需要手动 `Dispose()`
- 使用 `Allocator.Persistent` 或 `Allocator.TempJob`
- 注意生命周期管理

### 7.2 确定性
- 遍历顺序可能不确定（取决于添加顺序）
- 如果需要确定性，需要手动排序

### 7.3 性能权衡
- **紧密排列的优势**：批量处理时性能更好
- **映射的开销**：需要维护 Entity ID -> 索引的映射
- **总体评估**：在批量处理场景下，紧密排列的优势明显

## 八、总结

### ✅ 可行
可以使用 `NativeArray`/`NativeList` 实现紧密排列，而不使用 Unity ECS 的 Entity/System。

### 📝 推荐方案
**方案1：NativeArray + 映射**
- ✅ 紧密排列
- ✅ 独立性
- ✅ 性能好
- ⚠️ 需要手动管理内存

### 🎯 适用场景
- 需要批量处理大量组件
- 希望提高 CPU 缓存命中率
- 不想完全迁移到 Unity ECS

