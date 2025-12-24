# 帧同步服务器

## 协议格式

消息格式：`len(4 bytes) + messageType(1 byte) + byte[]`

- **len**: 4字节，大端序，表示消息总长度（包括 messageType 1字节 + 数据长度）
- **messageType**: 1字节，消息类型枚举值
- **byte[]**: 数据部分，使用 Protobuf 序列化

## 消息类型

- `MESSAGE_CONNECT (1)`: 客户端连接
- `MESSAGE_FRAME_DATA (2)`: 帧数据（上下左右输入）
- `MESSAGE_SERVER_FRAME (3)`: 服务器帧同步数据
- `MESSAGE_DISCONNECT (4)`: 断开连接

## 输入方向

- `DIRECTION_NONE (0)`: 无方向
- `DIRECTION_UP (1)`: 上
- `DIRECTION_DOWN (2)`: 下
- `DIRECTION_LEFT (3)`: 左
- `DIRECTION_RIGHT (4)`: 右

## 运行服务器

```bash
cd RollPredictServer
go build -o frame_sync_server frame_sync_server.go
./frame_sync_server
```

服务器默认监听端口：`8088`

## 客户端设置

### Unity 客户端

1. **安装 Google.Protobuf**
   - 通过 Unity Package Manager 安装，或
   - 下载 [Google.Protobuf NuGet 包](https://www.nuget.org/packages/Google.Protobuf) 并解压到 `Assets/Plugins/` 目录

2. **使用生成的 Proto 代码**
   - Proto 代码已生成在 `RollPredict/Assets/Proto/Game.cs`
   - 确保 `Game.cs` 文件在 Unity 项目中

3. **使用网络管理器**
   - 参考 `FrameSyncExample.cs` 的使用示例
   - 将 `FrameSyncNetwork` 组件添加到场景中

## 数据格式（Protobuf）

### 连接消息 (MESSAGE_CONNECT)

**客户端发送：**
```protobuf
ConnectMessage {
  player_id: "player_123"
  player_name: "PlayerName"
}
```

**服务器响应：**
```protobuf
ConnectMessage {
  player_id: "assigned_id"
  player_name: ""
}
```

### 帧数据 (MESSAGE_FRAME_DATA)

**客户端发送：**
```protobuf
FrameData {
  player_id: "player_123"
  direction: DIRECTION_UP
  is_pressed: true
  frame_number: 100
}
```

### 服务器帧 (MESSAGE_SERVER_FRAME)

**服务器发送：**
```protobuf
ServerFrame {
  frame_number: 100
  timestamp: 1234567890
  frame_datas: [
    {
      player_id: "player_123"
      direction: DIRECTION_UP
      is_pressed: true
      frame_number: 100
    }
  ]
}
```

## 帧同步机制

- 服务器以 20 FPS（每 50ms 一帧）的频率同步帧数据
- 客户端发送输入数据到服务器
- 服务器收集所有客户端的输入，然后广播给所有客户端
- 客户端根据服务器帧数据更新游戏状态

## 重新生成 Proto 代码

如果修改了 `proto/game.proto`，需要重新生成代码：

**Go 代码：**
```bash
cd RollPredictServer
protoc --go_out=. --go_opt=paths=source_relative proto/game.proto
```

**C# 代码：**
```bash
cd RollPredictServer
protoc --csharp_out=../RollPredict/Assets/Proto proto/game.proto
```
