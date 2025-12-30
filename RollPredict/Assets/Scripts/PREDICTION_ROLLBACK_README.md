# 预测回滚系统使用说明

## 概述

预测回滚（Prediction and Rollback）是一种网络游戏同步技术，用于减少延迟感知，提升游戏体验。

### 工作原理

1. **客户端预测（Client Prediction）**：
   - 客户端收到玩家输入后，立即执行，不等待服务器确认
   - 玩家操作感觉更流畅，延迟更低

2. **状态快照（State Snapshot）**：
   - 每一帧保存游戏状态的完整快照
   - 包括所有玩家的位置、旋转等信息

3. **回滚（Rollback）**：
   - 当收到服务器帧时，检查与本地预测是否一致
   - 如果不一致，回滚到服务器确认的帧
   - 然后重新执行所有输入，直到当前帧

## 核心组件

### 1. GameStateSnapshot（游戏状态快照）
- 保存每一帧的游戏状态
- 支持克隆和恢复

### 2. PredictionRollbackManager（预测回滚管理器）
- 管理状态快照历史
- 管理输入历史
- 处理回滚逻辑
- 协调预测和回滚

### 3. IGameLogicExecutor（游戏逻辑执行器接口）
- 定义游戏逻辑如何执行
- 必须实现 `ExecuteFrame` 方法

## 使用方法

### 1. 设置预测回滚管理器

在游戏对象上添加 `PredictionRollbackManager` 组件：

```csharp
PredictionRollbackManager predictionManager = gameObject.AddComponent<PredictionRollbackManager>();
predictionManager.enablePredictionRollback = true;
predictionManager.maxSnapshots = 100;
```

### 2. 实现游戏逻辑执行器

让你的游戏逻辑类实现 `IGameLogicExecutor` 接口：

```csharp
public class FrameSyncExample : MonoBehaviour, IGameLogicExecutor
{
    public void ExecuteFrame(Dictionary<int, InputDirection> inputs, long frameNumber)
    {
        // 根据输入执行游戏逻辑
        foreach (var kvp in inputs)
        {
            int playerId = kvp.Key;
            InputDirection direction = kvp.Value;
            // 更新玩家状态...
        }
    }
}
```

### 3. 注册玩家对象

在游戏开始时，注册所有玩家对象：

```csharp
predictionManager.RegisterPlayer(playerId, playerObject);
```

### 4. 客户端预测

当玩家输入时，立即进行预测：

```csharp
// 发送输入到服务器
networkManager.SendFrameData(direction);

// 客户端预测
localFrameNumber++;
predictionManager.PredictInput(playerId, direction, localFrameNumber);
```

### 5. 处理服务器帧

在收到服务器帧时，使用预测回滚管理器处理：

```csharp
private void OnServerFrameReceived(ServerFrame serverFrame)
{
    predictionManager.ProcessServerFrame(serverFrame);
}
```

## 配置选项

### PredictionRollbackManager

- **enablePredictionRollback**: 是否启用预测回滚（默认：true）
- **maxSnapshots**: 最大保存的快照数量（默认：100）

## 注意事项

1. **确定性**：游戏逻辑必须是确定性的，相同的输入必须产生相同的结果
2. **随机数**：使用服务器提供的随机种子，确保所有客户端使用相同的随机序列
3. **性能**：保存太多快照会占用内存，建议根据游戏需求调整 `maxSnapshots`
4. **回滚频率**：如果回滚过于频繁，可能是网络延迟过高或游戏逻辑不够确定性

## 示例

参考 `FrameSyncExample.cs` 查看完整示例。

## 工作原理详解

### 正常流程（无回滚）

1. 客户端收到输入 → 立即预测执行（帧N）
2. 发送输入到服务器
3. 服务器处理并广播（帧N）
4. 客户端收到服务器帧N → 检查一致 → 继续

### 回滚流程

1. 客户端收到输入 → 立即预测执行（帧N）
2. 发送输入到服务器
3. 服务器处理并广播（帧N），但输入与客户端预测不一致
4. 客户端收到服务器帧N → 检测到不一致
5. 回滚到帧N-1（服务器确认的最后一帧）
6. 使用服务器确认的输入重新执行帧N
7. 继续预测后续帧

## 调试

- 查看 `OnRollback` 事件来监控回滚发生
- 查看 `OnPrediction` 事件来监控预测发生
- 使用 `GetConfirmedFrame()` 和 `GetPredictedFrame()` 来查看当前帧状态

