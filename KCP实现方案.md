# KCP实现方案

## 一、KCP简介

KCP是一个快速可靠协议，可以比TCP浪费10%-20%的带宽的代价，换取平均延迟降低30%-40%，且最大延迟降低三倍的传输效果。

### 1.1 KCP特点

1. **基于UDP**：使用UDP作为底层传输
2. **可靠传输**：提供类似TCP的可靠性保证
3. **低延迟**：比TCP延迟更低
4. **可配置**：可以自定义重传策略、窗口大小等参数
5. **适合实时游戏**：特别适合帧同步等对延迟敏感的场景

### 1.2 KCP vs TCP

| 特性 | TCP | KCP |
|------|-----|-----|
| 传输层 | TCP | UDP |
| 可靠性 | 自动重传 | 可配置重传 |
| 延迟 | 较高 | 较低 |
| 带宽 | 较低 | 略高（10%-20%） |
| 适用场景 | 一般网络应用 | 实时游戏、视频通话 |

## 二、实现架构

### 2.1 整体架构

```
Unity客户端 (KCP) ←→ UDP ←→ Go服务器 (KCP)
```

### 2.2 消息格式

保持与TCP版本相同的消息格式：
```
len(4 bytes) + messageType(1 byte) + protobuf data
```

KCP会将这个数据包封装后通过UDP发送。

## 三、Go服务器端实现

### 3.1 依赖库

使用 `github.com/xtaci/kcp-go` 库：

```bash
go get github.com/xtaci/kcp-go/v5
```

### 3.2 实现步骤

1. 将TCP连接改为KCP连接
2. 保持消息格式不变
3. 配置KCP参数（窗口大小、重传策略等）

## 四、Unity客户端实现

### 4.1 依赖库

使用 `kcp2k` 库（Unity C# KCP实现）：

```bash
# 通过Unity Package Manager或Git URL添加
https://github.com/vis2k/kcp2k.git
```

或者使用 `KcpSharp`：
```bash
https://github.com/kcp-sharp/KcpSharp.git
```

### 4.2 实现步骤

1. 将TcpClient改为KcpClient
2. 保持消息格式不变
3. 配置KCP参数

## 五、KCP参数配置

### 5.1 推荐配置（帧同步游戏）

```csharp
// 快速模式（低延迟，适合帧同步）
KcpConfig config = new KcpConfig
{
    // 发送窗口大小
    SendWindowSize = 32,
    // 接收窗口大小
    ReceiveWindowSize = 32,
    // 最大传输单元（MTU）
    Mtu = 1400,
    // 是否启用快速重传
    FastResend = 2,
    // 是否启用无延迟模式
    NoDelay = true,
    // 内部更新间隔（毫秒）
    Interval = 10,
    // 快速重传触发阈值
    Resend = 2,
    // 是否启用流控
    Nocwnd = false,
    // 最小RTO（重传超时，毫秒）
    MinRto = 30,
};
```

### 5.2 参数说明

- **SendWindowSize/ReceiveWindowSize**：窗口大小，影响吞吐量和延迟
- **Mtu**：最大传输单元，通常1400（以太网MTU 1500 - IP头40）
- **FastResend**：快速重传阈值，收到多少个重复ACK后立即重传
- **NoDelay**：无延迟模式，立即发送数据
- **Interval**：内部更新间隔，越小延迟越低但CPU占用越高
- **Resend**：快速重传触发阈值
- **MinRto**：最小重传超时，防止过早重传

## 六、迁移步骤

### 6.1 服务器端迁移

1. 添加KCP依赖
2. 修改 `frame_sync_server.go`：
   - 将 `net.Listen("tcp", ...)` 改为 `kcp.Listen(...)`
   - 将 `net.Conn` 改为 `kcp.UDPSession`
   - 保持消息处理逻辑不变

### 6.2 客户端迁移

1. 添加KCP库（kcp2k或KcpSharp）
2. 修改 `FrameSyncNetwork.cs`：
   - 将 `TcpClient` 改为 `KcpClient`
   - 将 `NetworkStream` 改为KCP的流
   - 保持消息格式不变

### 6.3 测试

1. 先测试基本连接
2. 测试消息收发
3. 测试延迟和丢包恢复
4. 对比TCP和KCP的延迟差异

## 七、注意事项

### 7.1 防火墙和NAT

- UDP可能被防火墙阻止
- 需要处理NAT穿透（如果需要）
- 服务器需要开放UDP端口

### 7.2 调试

- KCP比TCP更难调试（UDP无连接）
- 需要添加日志记录连接状态
- 监控丢包率和延迟

### 7.3 兼容性

- 可以同时支持TCP和KCP（双协议）
- 客户端可以选择使用哪种协议
- 服务器可以同时监听TCP和UDP端口

## 八、Unity客户端KCP库选择

### 8.1 推荐库：kcp2k

**GitHub**: https://github.com/vis2k/kcp2k

**安装方法**：
1. 打开Unity Package Manager
2. 点击 `+` -> `Add package from git URL`
3. 输入：`https://github.com/vis2k/kcp2k.git?path=/kcp2k`

**优点**：
- 专为Unity设计
- 性能优秀
- 文档完善
- 活跃维护

### 8.2 替代方案：KcpSharp

**GitHub**: https://github.com/kcp-sharp/KcpSharp

**安装方法**：
- 通过NuGet或直接下载DLL

### 8.3 使用kcp2k的完整示例

```csharp
using kcp2k;

public class FrameSyncNetworkKCP : MonoBehaviour
{
    private KcpClient kcpClient;
    
    void Start()
    {
        // 创建KCP客户端
        kcpClient = new KcpClient(
            // 发送回调（通过UDP发送）
            (ArraySegment<byte> data) => {
                // 使用UdpClient发送数据
                udpClient.Send(data.Array, data.Count, serverEndPoint);
            },
            // 接收回调
            (ArraySegment<byte> data) => {
                OnKCPDataReceived(data);
            }
        );
        
        // 配置KCP参数
        kcpClient.NoDelay = true;
        kcpClient.Interval = 10;
        kcpClient.FastResend = 2;
        kcpClient.SendWindowSize = 32;
        kcpClient.ReceiveWindowSize = 32;
        kcpClient.Mtu = 1400;
        kcpClient.MinRto = 30;
        
        // 连接到服务器
        kcpClient.Connect(serverIP, serverPort);
    }
    
    void Update()
    {
        // 需要定期调用Tick更新KCP
        if (kcpClient != null)
        {
            kcpClient.Tick();
        }
    }
    
    void OnDestroy()
    {
        kcpClient?.Disconnect();
    }
}
```

## 九、性能对比

### 9.1 预期效果

- **延迟降低**：平均延迟降低30%-40%
- **最大延迟**：最大延迟降低3倍
- **带宽增加**：带宽增加10%-20%
- **CPU占用**：略高于TCP（但可接受）

### 9.2 测试方法

1. 使用 `network_simulator` 模拟网络延迟
2. 对比TCP和KCP的延迟
3. 测试不同网络条件下的表现

## 十、迁移检查清单

### 10.1 服务器端

- [ ] 添加 `github.com/xtaci/kcp-go/v5` 依赖
- [ ] 修改 `frame_sync_server.go` 使用KCP
- [ ] 或创建新的 `frame_sync_server_kcp.go`
- [ ] 测试KCP服务器启动
- [ ] 测试客户端连接

### 10.2 客户端

- [ ] 添加kcp2k库到Unity项目
- [ ] 创建 `FrameSyncNetworkKCP.cs`
- [ ] 修改连接逻辑使用KCP
- [ ] 保持消息格式不变
- [ ] 测试连接和消息收发

### 10.3 测试

- [ ] 基本连接测试
- [ ] 消息收发测试
- [ ] 延迟对比测试
- [ ] 丢包恢复测试
- [ ] 长时间运行稳定性测试

