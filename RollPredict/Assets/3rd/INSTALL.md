# 安装指南

## 方式一：直接导入（推荐用于开发）

1. 将整个 `Frame` 文件夹复制到你的 Unity 项目的 `Assets` 目录下
2. Unity 会自动检测并编译 Assembly Definition 文件
3. 等待 Unity 编译完成即可使用

**优点**：简单直接，适合开发和调试  
**缺点**：更新需要手动替换文件

## 方式二：作为 UPM 包（推荐用于分发）

### 本地包方式

1. 将 `Frame` 文件夹移动到 Unity 项目外的某个位置（例如：`~/UnityPackages/Frame`）
2. 在你的 Unity 项目中，打开 `Packages/manifest.json`
3. 添加本地包引用：

```json
{
  "dependencies": {
    "com.yourcompany.frame": "file:../path/to/Frame"
  }
}
```

**注意**：路径是相对于 `Packages` 文件夹的路径

### Git 包方式

如果你的代码托管在 Git 仓库中：

1. 在 `Packages/manifest.json` 中添加：

```json
{
  "dependencies": {
    "com.yourcompany.frame": "https://github.com/yourusername/frame.git?path=Assets/Scripts/Frame"
  }
}
```

### 本地文件系统包方式

1. 将 `Frame` 文件夹移动到 Unity 的本地包目录（可选）
2. 在 `Packages/manifest.json` 中添加：

```json
{
  "dependencies": {
    "com.yourcompany.frame": "file:/absolute/path/to/Frame"
  }
}
```

## 验证安装

安装完成后，在 Unity 编辑器中：

1. 打开 `Window > Package Manager`
2. 在左上角下拉菜单中选择 `In Project`
3. 应该能看到 `Frame` 包

或者在代码中测试：

```csharp
using UnityEngine;

public class TestFrame : MonoBehaviour
{
    void Start()
    {
        // 测试单例
        var instance = SingletonMono<TestFrame>.Instance;
        Debug.Log("Frame plugin installed successfully!");
    }
}
```

## 更新插件

### 直接导入方式
- 删除旧的 `Frame` 文件夹
- 复制新的 `Frame` 文件夹

### UPM 包方式
- 如果是 Git 包，Unity 会自动检查更新
- 如果是本地包，需要手动更新文件

## 卸载插件

### 直接导入方式
- 删除 `Assets/Scripts/Frame` 文件夹

### UPM 包方式
- 从 `Packages/manifest.json` 中删除对应的依赖项
- Unity 会自动移除包

## 常见问题

### Q: Assembly Definition 编译错误
**A**: 确保所有依赖的程序集都已正确配置。检查 `Frame.asmdef`、`Fix.asmdef` 和 `Physics.asmdef` 中的 `references` 字段。

### Q: 找不到命名空间
**A**: 确保：
- Assembly Definition 文件已正确创建
- 命名空间与代码中的 `namespace` 声明一致
- 其他程序集已正确引用本插件

### Q: 作为 UPM 包时找不到文件
**A**: 检查：
- `package.json` 中的路径是否正确
- 包文件夹结构是否符合 UPM 规范
- Unity 版本是否满足要求（2020.3+）

## 下一步

安装完成后，请查看 [README.md](README.md) 了解如何使用各个模块。

