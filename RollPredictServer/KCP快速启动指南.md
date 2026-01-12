# KCP服务器快速启动指南

## 一、安装依赖

```bash
cd RollPredictServer
go get github.com/xtaci/kcp-go/v5
```

## 二、启动服务器

### 方法1：使用启动脚本（推荐）

```bash
./启动KCP服务器.sh
```

### 方法2：手动编译运行

```bash
# 编译
go build -o frame_sync_server frame_sync_server.go frame_sync_server_kcp.go

# 运行
./frame_sync_server
```

### 方法3：直接运行（开发时）

```bash
go run frame_sync_server.go frame_sync_server_kcp.go
```

## 三、服务器端口

- **TCP服务器**：`:8089`（兼容旧客户端）
- **KCP服务器**：`:8088`（新客户端使用）

服务器会同时启动TCP和KCP，客户端可以选择使用哪种协议。

## 四、验证服务器运行

启动成功后，你应该看到：

```
KCP Frame Sync Server started on :8088
TCP Frame Sync Server started on :8089
```

## 五、Unity客户端连接

1. 在Unity场景中使用 `FrameSyncNetworkKCP` 组件
2. 配置服务器地址：`127.0.0.1`
3. 配置服务器端口：`8088`（KCP端口）
4. 调用 `Connect()` 方法连接

## 六、常见问题

### 6.1 编译错误：找不到kcp-go包

**解决**：
```bash
go get github.com/xtaci/kcp-go/v5
```

### 6.2 端口被占用

**解决**：
- 检查端口8088和8089是否被占用
- 修改 `frame_sync_server_kcp.go` 中的端口常量

### 6.3 客户端连接失败

**检查**：
- 服务器是否正常启动
- 防火墙是否阻止UDP端口8088
- 客户端地址和端口是否正确

## 七、测试

### 7.1 基本测试

1. 启动服务器
2. Unity客户端连接
3. 测试消息收发

### 7.2 性能测试

使用 `network_simulator` 模拟网络延迟：

```bash
# 启动网络模拟器
./network_simulator -delay 100 -listen 8090 -target 127.0.0.1:8088

# Unity客户端连接到 127.0.0.1:8090（通过代理）
```

## 八、日志

服务器会输出以下日志：
- 客户端连接：`KCP Client X connected from ...`
- 客户端断开：`KCP Client X disconnected`
- 消息处理：`Client X: frame data`
- 错误信息：`KCP Client X: Read error: ...`

