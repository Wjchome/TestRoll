# Frame - Unity 框架插件

一个用于 Unity 开发的通用框架插件，包含基础工具类、定点数数学库和物理系统。

## 📦 模块说明

### 1. 基础框架 (Frame)
- **Singleton**: 线程安全的单例模式实现
- **SingletonMono**: Unity MonoBehaviour 单例基类
- **ObjectPool**: 泛型对象池，支持 MonoBehaviour 对象复用

### 2. 定点数数学库 (Fix)
基于 `FixMath.NET` 的定点数实现，用于帧同步游戏开发：
- **Fix64**: 64位定点数（Q31.32格式）
- **Fix64Extensions**: 定点数扩展方法
- **FixRandom**: 定点数随机数生成器
- 支持三角函数、对数等数学运算

### 3. 物理系统 (Physics)
基于定点数的 2D/3D 物理引擎，适用于帧同步游戏：

#### Physics2D
- **PhysicsWorld2D**: 2D 物理世界管理
- **RigidBody2D**: 2D 刚体（支持平移运动）
- **CollisionShape2D**: 碰撞形状（Box、Circle）
- **QuadTree**: 四叉树空间分割，优化碰撞检测
- **PhysicsLayer**: 物理层系统（类似 Unity LayerMask）

#### Physics3D
- **PhysicsWorld3D**: 3D 物理世界管理
- **RigidBody3D**: 3D 刚体（支持平移运动）
- **CollisionShape3D**: 碰撞形状（Box、Sphere）
- **BVH**: 层次包围盒树，优化碰撞检测

## 🚀 快速开始

### 安装方式

#### 方式一：直接导入（推荐）
1. 将整个 `Frame` 文件夹复制到你的 Unity 项目的 `Assets` 目录下
2. Unity 会自动编译 Assembly Definition 文件

#### 方式二：作为 UPM 包（可选）
如果需要作为 Unity Package Manager 包使用，可以创建 `package.json`：

```json
{
  "name": "com.yourcompany.frame",
  "version": "1.0.0",
  "displayName": "Frame",
  "description": "Unity 框架插件",
  "unity": "2020.3",
  "dependencies": {}
}
```

### 使用示例

#### 1. 使用单例模式

```csharp
using Frame.Core;

// 普通单例
public class GameManager : Singleton
{
    // 使用 GameManager.Instance 访问
}

// MonoBehaviour 单例
public class AudioManager : SingletonMono<AudioManager>
{
    // 使用 AudioManager.Instance 访问
    // 会自动创建 GameObject 如果不存在
}
```

#### 2. 使用对象池

```csharp
using Frame.Core;

public class Bullet : MonoBehaviour { }

// 创建对象池
ObjectPool<Bullet> bulletPool = new ObjectPool<Bullet>(
    bulletPrefab,
    onSpawn: (bullet) => bullet.gameObject.SetActive(true),
    onDespawn: (bullet) => bullet.gameObject.SetActive(false)
);

// 获取对象
Bullet bullet = bulletPool.GetObject();

// 归还对象
bulletPool.ReturnObject(bullet);
```

#### 3. 使用定点数

```csharp
using Frame.FixMath;

Fix64 a = (Fix64)1.5;
Fix64 b = (Fix64)2.0;
Fix64 result = a + b; // 3.5

// 转换为 float
float floatValue = (float)result;
```

#### 4. 使用物理系统

```csharp
using Frame.Physics2D;
using Frame.FixMath;

// 创建物理世界
PhysicsWorld2D world = new PhysicsWorld2D();
world.Gravity = new FixVector2(0, (Fix64)(-9.81));

// 创建刚体
RigidBody2D body = new RigidBody2D();
body.Position = new FixVector2(0, 10);
body.Velocity = new FixVector2(5, 0);
body.Mass = (Fix64)1.0;
body.Shape = new BoxShape2D(new FixVector2(1, 1));

// 添加到世界
world.AddBody(body);

// 更新物理模拟
world.Step((Fix64)Time.fixedDeltaTime);
```

#### 5. 使用 Unity 组件

```csharp
// 在场景中添加 PhysicsWorld2DComponent
// 它会自动创建单例并管理物理世界

// 添加刚体组件
RigidBody2DComponent rb = gameObject.AddComponent<RigidBody2DComponent>();
rb.Initialize(world, new FixVector2(0, 0));
```

## 📋 依赖关系

```
Frame (基础框架)
  └─ UnityEngine.CoreModule

Fix (定点数库)
  └─ UnityEngine.CoreModule

Physics (物理系统)
  ├─ UnityEngine.CoreModule
  └─ Fix (依赖定点数库)
```

## ⚙️ 配置说明

### Assembly Definition 文件
插件使用 Assembly Definition 文件来管理程序集：
- `Frame.asmdef`: 基础框架程序集
- `Fix.asmdef`: 定点数库程序集
- `Physics.asmdef`: 物理系统程序集（依赖 Fix）

### 命名空间
- 基础框架：`Frame.Core`
- 定点数库：`Frame.FixMath`
- 物理系统：`Frame.Physics2D`、`Frame.Physics3D`

## 🎯 适用场景

- ✅ 帧同步多人游戏
- ✅ 需要确定性物理模拟
- ✅ 跨平台一致性要求高的游戏
- ✅ 需要对象池优化的项目

## ⚠️ 注意事项

1. **定点数精度**：Fix64 的精度约为 2^-32，对于极高精度需求可能需要考虑其他方案
2. **性能**：定点数运算比浮点数稍慢，但提供了确定性
3. **物理系统**：当前版本只支持平移运动，不支持旋转和力矩
4. **测试代码**：`Fix/MyTest` 和 `Physics/Demo` 文件夹包含测试代码，可以删除

## 📝 版本要求

- Unity 2020.3 或更高版本
- .NET Standard 2.1 或更高版本

## 📄 许可证

请根据你的项目需求添加相应的许可证信息。

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

---

**注意**：这是一个框架插件，可以根据项目需求进行定制和扩展。

