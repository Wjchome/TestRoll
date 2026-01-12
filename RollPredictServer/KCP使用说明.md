# KCP使用说明

## 一、安装依赖

### 1.1 添加KCP库

```bash
cd RollPredictServer
go get github.com/xtaci/kcp-go/v5
```

### 1.2 更新go.mod

运行上述命令后，`go.mod`会自动更新，添加KCP依赖。

## 二、启动服务器

### 2.1 只启动KCP服务器

修改 `main.go`：

```go
func main() {
    server := NewServer()
    server.StartKCP()  // 只启动KCP服务器
}
```

### 2.2 同时启动TCP和KCP服务器（推荐）

修改 `main.go`：

```go
func main() {
    server := NewServer()
    server.StartBoth()  // 同时启动TCP和KCP，兼容旧客户端
}
```

这样：
- TCP服务器监听 `:8089`（旧客户端）
- KCP服务器监听 `:8088`（新客户端）

## 三、客户端配置

### 3.1 Unity客户端使用KCP

1. **添加kcp2k库**：
   - 打开Unity Package Manager
   - 点击 `+` -> `Add package from git URL`
   - 输入：`https://github.com/vis2k/kcp2k.git?path=/kcp2k`

2. **使用FrameSyncNetworkKCP**：
   - 在场景中使用 `FrameSyncNetworkKCP` 组件
   - 配置服务器地址和端口（KCP端口：8088）
   - 配置KCP参数（使用默认值即可）

3. **测试连接**：
   - 运行游戏，点击连接
   - 查看日志确认KCP连接成功

## 四、KCP参数调优

### 4.1 快速模式（推荐，低延迟）

```go
conn.SetNoDelay(1, 10, 2, 1)  // nodelay, interval, resend, nc
conn.SetWindowSize(128, 128)   // send window, recv window
conn.SetMtu(1400)              // MTU
conn.SetACKNoDelay(true)       // 立即发送ACK
```

### 4.2 平衡模式（延迟和带宽平衡）

```go
conn.SetNoDelay(0, 40, 0, 0)   // 标准模式
conn.SetWindowSize(256, 256)   // 更大的窗口
conn.SetMtu(1400)
conn.SetACKNoDelay(false)      // 延迟ACK
```

### 4.3 参数说明

- **NoDelay(1, 10, 2, 1)**：
  - `1`：启用nodelay模式
  - `10`：内部更新间隔（毫秒）
  - `2`：快速重传阈值
  - `1`：禁用流控

- **WindowSize(128, 128)**：
  - 发送窗口：128个包
  - 接收窗口：128个包
  - 越大吞吐量越高，但延迟可能增加

- **MTU 1400**：
  - 以太网MTU 1500 - IP头40 = 1460
  - 使用1400留有余量

## 五、测试

### 5.1 基本测试

1. 启动KCP服务器
2. Unity客户端连接
3. 测试消息收发
4. 查看延迟

### 5.2 对比测试

1. 同时启动TCP和KCP服务器
2. 分别使用TCP和KCP客户端连接
3. 对比延迟差异
4. 测试丢包恢复能力

### 5.3 网络模拟测试

使用 `network_simulator` 模拟网络延迟：

```bash
# 测试KCP在不同延迟下的表现
./network_simulator -delay 50
./network_simulator -delay 100
./network_simulator -delay 200
```

## 六、常见问题

### 6.1 连接失败

- 检查防火墙是否阻止UDP端口
- 确认服务器端口正确（8088）
- 查看服务器日志

### 6.2 延迟没有降低

- 检查KCP参数配置
- 确认使用的是KCP而不是TCP
- 测试网络环境（本地网络延迟本身就很低）

### 6.3 丢包率高

- 增加窗口大小
- 调整快速重传参数
- 检查网络质量

## 七、性能监控

### 7.1 服务器端监控

可以添加统计信息：

```go
// 获取KCP统计信息
stats := conn.GetStats()
fmt.Printf("Sent: %d, Received: %d, Lost: %d\n", 
    stats.Sent, stats.Received, stats.Lost)
```

### 7.2 客户端监控

在Unity中添加延迟显示：

```csharp
// 记录发送和接收时间
long sendTime = DateTime.Now.Ticks;
// ... 发送消息 ...
long receiveTime = DateTime.Now.Ticks;
long latency = (receiveTime - sendTime) / 10000; // 转换为毫秒
Debug.Log($"Latency: {latency}ms");
```

