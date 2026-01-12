# KCP客户端使用说明

## 一、已完成的实现

### 1.1 客户端实现

`FrameSyncNetworkKCP.cs` 已经完成KCP客户端实现：

- ✅ 使用 `System.Net.Sockets.Kcp` 库
- ✅ UDP作为底层传输
- ✅ KCP协议封装
- ✅ 消息格式与TCP版本一致
- ✅ 自动更新KCP状态

### 1.2 服务器端实现

`frame_sync_server_kcp.go` 已经完成KCP服务器实现：

- ✅ 使用 `github.com/xtaci/kcp-go/v5` 库
- ✅ UDP监听
- ✅ KCP协议封装
- ✅ 消息格式与TCP版本一致

## 二、使用方法

### 2.1 服务器端

1. **安装依赖**：
   ```bash
   cd RollPredictServer
   go get github.com/xtaci/kcp-go/v5
   ```

2. **修改main.go**：
   ```go
   func main() {
       server := NewServer()
       server.StartBoth()  // 同时支持TCP和KCP
       // 或者
       // server.StartKCP()  // 只启动KCP
   }
   ```

3. **启动服务器**：
   ```bash
   go build -o frame_sync_server
   ./frame_sync_server
   ```

### 2.2 Unity客户端

1. **在场景中使用**：
   - 添加 `FrameSyncNetworkKCP` 组件到GameObject
   - 配置服务器地址和端口（KCP端口：8088）

2. **配置KCP参数**（使用默认值即可）：
   - Send Window Size: 32
   - Receive Window Size: 32
   - MTU: 1400
   - No Delay: true
   - Interval: 10ms

3. **连接服务器**：
   ```csharp
   FrameSyncNetworkKCP.Instance.Connect();
   ```

4. **发送数据**：
   ```csharp
   var frameData = new FrameData { ... };
   FrameSyncNetworkKCP.Instance.SendFrameData(frameData);
   ```

## 三、KCP参数说明

### 3.1 客户端参数

- **Send Window Size (32)**：发送窗口大小，影响吞吐量
- **Receive Window Size (32)**：接收窗口大小，必须大于最大分片数
- **MTU (1400)**：最大传输单元，通常1400（以太网MTU 1500 - IP头40）
- **No Delay (true)**：无延迟模式，立即发送数据
- **Interval (10ms)**：内部更新间隔，越小延迟越低但CPU占用越高
- **Fast Resend (2)**：快速重传阈值
- **Resend (2)**：快速重传触发阈值

### 3.2 服务器端参数

服务器端使用快速模式配置：
```go
conn.SetNoDelay(1, 10, 2, 1)  // nodelay, interval, resend, nc
conn.SetWindowSize(128, 128)   // send window, recv window
conn.SetMtu(1400)              // MTU
conn.SetACKNoDelay(true)       // 立即发送ACK
conn.SetStreamMode(false)      // 非流模式（数据包模式）
```

## 四、注意事项

### 4.1 Conv（会话ID）

- 客户端使用固定的conv：`2001`
- 服务器端kcp-go库会自动处理conv，不需要手动设置
- 如果连接失败，检查防火墙是否阻止UDP端口

### 4.2 消息格式

保持与TCP版本一致：
```
len(4 bytes, BigEndian) + messageType(1 byte) + protobuf data
```

### 4.3 线程安全

- KCP的Update需要在主线程或固定线程中调用
- 当前实现在Unity的Update中调用
- 接收数据在后台线程处理，通过消息队列传递到主线程

### 4.4 性能优化

- KCP的Update频率建议10-20ms一次
- 当前实现在Unity的Update中调用（通常60fps，约16ms一次）
- 如果需要更精确的控制，可以使用协程或固定时间间隔

## 五、测试

### 5.1 基本测试

1. 启动KCP服务器
2. Unity客户端连接
3. 测试消息收发
4. 查看日志确认连接成功

### 5.2 延迟测试

使用 `network_simulator` 模拟网络延迟：

```bash
# 测试KCP在不同延迟下的表现
./network_simulator -delay 50 -listen 8090 -target 127.0.0.1:8088
```

然后Unity客户端连接到 `127.0.0.1:8090`

### 5.3 对比测试

1. 同时启动TCP和KCP服务器
2. 分别使用TCP和KCP客户端连接
3. 对比延迟差异
4. 测试丢包恢复能力

## 六、常见问题

### 6.1 连接失败

- **检查防火墙**：UDP端口可能被防火墙阻止
- **检查端口**：确认服务器端口正确（8088）
- **查看日志**：检查服务器和客户端日志

### 6.2 消息收不到

- **检查KCP Update**：确保Update被定期调用
- **检查网络**：使用网络抓包工具检查UDP数据包
- **检查消息格式**：确认消息格式正确

### 6.3 延迟没有降低

- **检查KCP参数**：确认使用了快速模式
- **测试网络环境**：本地网络延迟本身就很低
- **对比测试**：与TCP版本对比延迟差异

## 七、性能监控

### 7.1 客户端监控

可以在Unity中添加延迟显示：

```csharp
// 记录发送和接收时间
long sendTime = DateTime.Now.Ticks;
// ... 发送消息 ...
long receiveTime = DateTime.Now.Ticks;
long latency = (receiveTime - sendTime) / 10000; // 转换为毫秒
Debug.Log($"KCP Latency: {latency}ms");
```

### 7.2 服务器端监控

可以添加统计信息（需要扩展kcp-go库）：

```go
// 获取KCP统计信息（如果kcp-go支持）
// stats := conn.GetStats()
// fmt.Printf("Sent: %d, Received: %d\n", stats.Sent, stats.Received)
```

## 八、与TCP版本对比

### 8.1 代码差异

- **连接方式**：TCP使用TcpClient，KCP使用UdpClient + KCP封装
- **消息格式**：完全相同
- **API接口**：基本相同，可以无缝切换

### 8.2 性能差异

- **延迟**：KCP平均延迟降低30%-40%
- **最大延迟**：KCP最大延迟降低3倍
- **带宽**：KCP带宽增加10%-20%
- **CPU占用**：KCP略高于TCP（但可接受）

## 九、迁移建议

### 9.1 渐进式迁移

1. **第一阶段**：同时支持TCP和KCP（使用StartBoth）
2. **第二阶段**：测试KCP性能和稳定性
3. **第三阶段**：根据测试结果决定是否完全切换到KCP

### 9.2 兼容性

- 可以同时运行TCP和KCP服务器
- 客户端可以选择使用TCP或KCP
- 消息格式完全一致，业务逻辑无需修改

