package main

import (
	"bufio"
	"encoding/binary"
	"fmt"
	"log"
	"net"
	"strconv"
	"sync"
	"time"

	myproto "github.com/WjcHome/gohello/proto"
	"google.golang.org/protobuf/proto"
)

const (
	FRAME_INTERVAL = 200 * time.Millisecond // 20帧每秒
	PORT           = ":8088"
	MAX_PLAYERS    = 2 // 每个房间最大玩家数
)

// 全局客户端计数器
var clientCounter int64 = 0

// 客户端结构
type Client struct {
	ID       int32
	Conn     net.Conn
	RoomID   string
	Name     string
	IsHost   bool
	LastSeen time.Time
}

// 房间结构
type Room struct {
	ID              string
	Name            string
	HostID          int32
	Clients         map[int32]*Client
	FrameDataBuffer []*myproto.FrameData // 帧数据缓冲区
	FrameNumber     int64
	Status          string // "waiting", "playing"
	MaxPlayers      int32
	HistoryFrames   []*myproto.ServerFrame // 历史帧数据，用于补帧
	Mutex           sync.Mutex
}

// 服务器结构
type Server struct {
	Rooms map[string]*Room
	Mutex sync.Mutex
}

// 创建新服务器
func NewServer() *Server {
	return &Server{
		Rooms: make(map[string]*Room),
	}
}

// 启动服务器
func (s *Server) Start() {
	// 启动定期清理任务
	go s.cleanupEmptyRooms()

	ln, err := net.Listen("tcp", PORT)
	if err != nil {
		log.Fatal(err)
	}
	defer ln.Close()

	fmt.Printf("Frame Sync Server started on %s\n", PORT)

	for {
		conn, err := ln.Accept()
		if err != nil {
			log.Println("Accept error:", err)
			continue
		}
		go s.handleClient(conn)
	}
}

// 处理客户端连接
func (s *Server) handleClient(conn net.Conn) {
	defer conn.Close()

	// 禁用Nagle算法，减少网络延迟
	if tcpConn, ok := conn.(*net.TCPConn); ok {
		tcpConn.SetNoDelay(true)
	}

	clientID := int32(clientCounter)
	clientCounter++
	client := &Client{
		ID:       clientID,
		Conn:     conn,
		LastSeen: time.Now(),
	}

	fmt.Printf("Client %d connected\n", client.ID)

	// 发送连接成功消息
	connectMsg := &myproto.ConnectMessage{
		PlayerId:   clientID,
		PlayerName: "",
	}
	s.sendMessage(conn, myproto.MessageType_MESSAGE_CONNECT, connectMsg)

	s.autoAssignRoom(client)

	reader := bufio.NewReader(conn)
	for {
		// 读取消息长度 (4 bytes)
		lengthBytes := make([]byte, 4)
		_, err := reader.Read(lengthBytes)
		if err != nil {
			log.Printf("Client %d: Read length error: %v\n", client.ID, err)
			break
		}
		length := binary.BigEndian.Uint32(lengthBytes)

		// 读取消息类型 (1 byte)
		messageTypeBytes := make([]byte, 1)
		_, err = reader.Read(messageTypeBytes)
		if err != nil {
			log.Printf("Client %d: Read message type error: %v\n", client.ID, err)
			break
		}
		messageType := myproto.MessageType(messageTypeBytes[0])

		// 读取数据部分 (length - 1 byte for messageType)
		dataLength := int(length) - 1
		if dataLength < 0 {
			log.Printf("Client %d: Invalid message length\n", client.ID)
			break
		}
		data := make([]byte, dataLength)
		_, err = reader.Read(data)
		if err != nil {
			log.Printf("Client %d: Read data error: %v\n", client.ID, err)
			break
		}

		// 更新最后活跃时间
		client.LastSeen = time.Now()

		// 根据消息类型处理
		switch messageType {
		case myproto.MessageType_MESSAGE_FRAME_DATA:
			s.handleFrameData(client, data)
		case myproto.MessageType_MESSAGE_DISCONNECT:
			s.handleDisconnect(client, data)
		case myproto.MessageType_MESSAGE_FRAME_LOSS:
			s.handleFrameLoss(client, data)
		default:
			log.Printf("Client %d: Unknown message type: %d\n", client.ID, messageType)
		}
	}

	// 客户端断开连接
	s.handleClientDisconnect(client)
}

// 处理帧数据
func (s *Server) handleFrameData(client *Client, data []byte) {
	if client.RoomID == "" {
		fmt.Printf("Client %d no room  %s\n", client.ID, client.Name)
		return
	}

	s.Mutex.Lock()
	room, exists := s.Rooms[client.RoomID]
	s.Mutex.Unlock()

	if !exists {
		log.Printf("Client %d: Room not found: %s\n", client.ID, client.RoomID)
		return
	}

	room.Mutex.Lock()
	// 只有游戏开始后才能接收帧数据
	if room.Status != "playing" {
		room.Mutex.Unlock()
		fmt.Printf("Client %d: Game not started yet, ignoring frame data\n", client.ID)
		return
	}
	room.Mutex.Unlock()

	var frameData myproto.FrameData
	if err := proto.Unmarshal(data, &frameData); err != nil {
		log.Printf("Client %d: Unmarshal frame data error: %v\n", client.ID, err)
		return
	}

	// 确保player_id正确
	if frameData.PlayerId == 0 {
		frameData.PlayerId = client.ID
	}

	room.Mutex.Lock()
	// 将客户端的帧数据添加到房间的缓冲区
	log.Printf("Client %d: frame data\n", client.ID)

	room.FrameDataBuffer = append(room.FrameDataBuffer, &frameData)
	room.Mutex.Unlock()
}

// 处理断开连接消息
func (s *Server) handleDisconnect(client *Client, data []byte) {
	fmt.Printf("Client %d requested disconnect\n", client.ID)
	s.handleClientDisconnect(client)
}

// 处理补帧请求
func (s *Server) handleFrameLoss(client *Client, data []byte) {
	if client.RoomID == "" {
		fmt.Printf("Client %d: No room assigned\n", client.ID)
		return
	}

	s.Mutex.Lock()
	room, exists := s.Rooms[client.RoomID]
	s.Mutex.Unlock()

	if !exists {
		log.Printf("Client %d: Room not found: %s\n", client.ID, client.RoomID)
		return
	}

	var lossFrameRequest myproto.GetLossFrame
	if err := proto.Unmarshal(data, &lossFrameRequest); err != nil {
		log.Printf("Client %d: Unmarshal frame loss request error: %v\n", client.ID, err)
		return
	}

	confirmedFrame := lossFrameRequest.LastFrameNumber
	currentFrame := int64(0)
	historyFramesLen := 0

	room.Mutex.Lock()
	currentFrame = room.FrameNumber
	historyFramesLen = len(room.HistoryFrames)
	// 计算需要补发的帧范围：[confirmed+1, current]
	// 帧号从1开始，HistoryFrames[i] 对应帧号 i+1
	// 所以帧号 frameNumber 对应的索引是 frameNumber - 1
	startIndex := confirmedFrame // confirmedFrame+1 对应的索引
	endIndex := currentFrame - 1 // currentFrame 对应的索引
	room.Mutex.Unlock()

	// 边界检查
	if startIndex < 0 {
		startIndex = 0
	}
	if endIndex >= int64(historyFramesLen) {
		endIndex = int64(historyFramesLen) - 1
	}
	if startIndex > endIndex || endIndex < 0 {
		// 不需要补帧或无效范围
		fmt.Printf("Client %d: No frames to send (confirmed: %d, current: %d, historyLen: %d)\n",
			client.ID, confirmedFrame, currentFrame, historyFramesLen)
		return
	}

	// 直接通过索引范围获取帧数据（使用切片操作，更高效）
	room.Mutex.Lock()
	framesSlice := room.HistoryFrames[startIndex : endIndex+1]
	// 需要复制一份，避免在锁外访问时数据被修改
	framesToSend := make([]*myproto.ServerFrame, len(framesSlice))
	copy(framesToSend, framesSlice)
	room.Mutex.Unlock()

	if len(framesToSend) > 0 {
		// 构建补帧消息
		sendAllFrame := &myproto.SendAllFrame{
			AllNeedFrame: framesToSend,
		}

		// 发送给请求的客户端
		s.sendMessage(client.Conn, myproto.MessageType_MESSAGE_FRAME_NEED, sendAllFrame)
		fmt.Printf("Client %d: Sent %d frames (from %d to %d)\n", client.ID, len(framesToSend), confirmedFrame+1, currentFrame)
	} else {
		fmt.Printf("Client %d: No frames to send (confirmed: %d, current: %d)\n", client.ID, confirmedFrame, currentFrame)
	}
}

// 处理客户端断开
func (s *Server) handleClientDisconnect(client *Client) {
	fmt.Printf("Client %d disconnected\n", client.ID)

	if client.RoomID == "" {
		return
	}

	s.Mutex.Lock()
	room, exists := s.Rooms[client.RoomID]
	s.Mutex.Unlock()

	if !exists {
		return
	}

	room.Mutex.Lock()
	delete(room.Clients, client.ID)
	client.RoomID = ""

	// 如果房主离开，选择新的房主
	if room.HostID == client.ID && len(room.Clients) > 0 {
		for _, c := range room.Clients {
			c.IsHost = true
			room.HostID = c.ID
			fmt.Printf("New host selected: %d in room %s\n", c.ID, room.ID)
			break
		}
	}

	// 如果房间空了，删除房间
	if len(room.Clients) == 0 {
		room.Mutex.Unlock()
		s.Mutex.Lock()
		delete(s.Rooms, room.ID)
		s.Mutex.Unlock()
		fmt.Printf("Room %s deleted (empty after disconnect)\n", room.ID)
		return
	}

	room.Mutex.Unlock()
	fmt.Printf("Client %d disconnected from room %s, %d players remaining\n", client.ID, room.ID, len(room.Clients))
}

// 创建房间
func (s *Server) CreateRoom(client *Client, roomName string, maxPlayers int32) string {
	roomID := strconv.FormatInt(int64(len(s.Rooms)+1), 10)
	if roomName == "" {
		roomName = fmt.Sprintf("Room %s", roomID)
	}

	room := &Room{
		ID:              roomID,
		Name:            roomName,
		HostID:          client.ID,
		Clients:         make(map[int32]*Client),
		FrameDataBuffer: make([]*myproto.FrameData, 0),
		Status:          "waiting",
		MaxPlayers:      maxPlayers,
		HistoryFrames:   make([]*myproto.ServerFrame, 0),
	}

	s.Mutex.Lock()
	s.Rooms[roomID] = room
	s.Mutex.Unlock()

	// 将客户端加入房间
	client.RoomID = roomID
	client.IsHost = true
	room.Clients[client.ID] = client

	fmt.Printf("Client %d created room %s (%s)\n", client.ID, roomID, roomName)
	return roomID
}

// 加入房间
func (s *Server) JoinRoom(client *Client, roomID string) bool {
	s.Mutex.Lock()
	room, exists := s.Rooms[roomID]
	s.Mutex.Unlock()

	if !exists {
		return false
	}

	room.Mutex.Lock()
	defer room.Mutex.Unlock()

	if room.Status != "waiting" {
		return false
	}

	if int32(len(room.Clients)) >= room.MaxPlayers {
		return false
	}

	// 加入房间
	client.RoomID = roomID
	room.Clients[client.ID] = client

	fmt.Printf("Client %d joined room %s (%d/%d players)\n", client.ID, roomID, len(room.Clients), room.MaxPlayers)

	// 检查是否达到人数上限，如果达到则开始游戏
	if int32(len(room.Clients)) >= room.MaxPlayers {
		fmt.Printf("Room %s is full, starting game...\n", roomID)
		go func() {
			time.Sleep(100 * time.Millisecond) // 稍微延迟，确保所有客户端都收到加入消息
			s.startGame(roomID)
		}()
	}

	return true
}

// 自动分配房间：查找等待中的房间或创建新房间
func (s *Server) autoAssignRoom(client *Client) {
	var targetRoomID string

	// 第一步：查找等待中的房间（需要先获取锁）
	s.Mutex.Lock()
	for _, room := range s.Rooms {
		room.Mutex.Lock()
		if room.Status == "waiting" && int32(len(room.Clients)) < room.MaxPlayers {
			targetRoomID = room.ID
			room.Mutex.Unlock()
			break
		}
		room.Mutex.Unlock()
	}
	s.Mutex.Unlock()

	// 如果找到可用房间，尝试加入（此时已经释放了s.Mutex，可以安全调用JoinRoom）
	if targetRoomID != "" {
		if s.JoinRoom(client, targetRoomID) {
			return
		}
	}

	// 第二步：没有找到可用房间，创建新房间
	s.Mutex.Lock()
	roomID := strconv.FormatInt(int64(len(s.Rooms)+1), 10)
	roomName := fmt.Sprintf("Room %s", roomID)

	room := &Room{
		ID:              roomID,
		Name:            roomName,
		HostID:          client.ID,
		Clients:         make(map[int32]*Client),
		FrameDataBuffer: make([]*myproto.FrameData, 0),
		Status:          "waiting",
		MaxPlayers:      MAX_PLAYERS,
		HistoryFrames:   make([]*myproto.ServerFrame, 0),
	}

	s.Rooms[roomID] = room
	s.Mutex.Unlock()

	// 将客户端加入房间
	room.Mutex.Lock()
	client.RoomID = roomID
	client.IsHost = true
	room.Clients[client.ID] = client
	room.Mutex.Unlock()

	fmt.Printf("Client %d created room %s (%s) (%d/%d players)\n", client.ID, roomID, roomName, 1, room.MaxPlayers)

	// 如果房间人数达到上限（包括测试情况：1人时也开始游戏），自动开始游戏
	room.Mutex.Lock()
	shouldStart := int32(len(room.Clients)) >= room.MaxPlayers
	room.Mutex.Unlock()

	if shouldStart {
		fmt.Printf("Room %s reached max players (%d/%d), starting game...\n", roomID, MAX_PLAYERS, room.MaxPlayers)
		go func() {
			time.Sleep(100 * time.Millisecond) // 稍微延迟，确保客户端收到加入消息
			s.startGame(roomID)
		}()
	}
}

// 开始游戏
func (s *Server) startGame(roomID string) {
	s.Mutex.Lock()
	room, exists := s.Rooms[roomID]
	s.Mutex.Unlock()

	if !exists {
		return
	}

	room.Mutex.Lock()
	if room.Status != "waiting" {
		room.Mutex.Unlock()
		return
	}

	room.Status = "playing"

	// 收集玩家ID列表
	playerIDs := make([]int32, 0, len(room.Clients))
	for _, c := range room.Clients {
		playerIDs = append(playerIDs, c.ID)
	}

	// 生成随机种子
	randomSeed := time.Now().UnixNano()

	// 构建游戏开始消息
	gameStart := &myproto.GameStart{
		RoomId:     roomID,
		RandomSeed: randomSeed,
		PlayerIds:  playerIDs,
	}

	room.Mutex.Unlock()

	// 发送游戏开始消息给所有客户端
	for _, client := range room.Clients {
		s.sendMessage(client.Conn, myproto.MessageType_MESSAGE_GAME_START, gameStart)
	}

	fmt.Printf("Game started in room %s with %d players (seed: %d)\n", roomID, len(playerIDs), randomSeed)

	// 延迟启动房间帧循环
	go func() {
		time.Sleep(200 * time.Millisecond) // 等待客户端收到游戏开始消息
		room.frameLoop(s)
	}()
}

// 发送消息（格式：len + messageType + byte[]）
func (s *Server) sendMessage(conn net.Conn, messageType myproto.MessageType, msg proto.Message) {
	data, err := proto.Marshal(msg)
	if err != nil {
		log.Printf("Marshal error: %v\n", err)
		return
	}

	// 计算总长度：1 byte (messageType) + data length
	totalLength := uint32(1 + len(data))

	// 写入长度 (4 bytes, big endian)
	lengthBytes := make([]byte, 4)
	binary.BigEndian.PutUint32(lengthBytes, totalLength)

	// 组合完整消息到一个缓冲区（虽然TCP是流式协议，但合并写入更高效且一致）
	message := make([]byte, 4+1+len(data))
	copy(message[0:4], lengthBytes)
	message[4] = byte(messageType)
	copy(message[5:], data)

	// 一次性写入完整消息
	_, err = conn.Write(message)
	if err != nil {
		log.Printf("TCP Write error: %v\n", err)
		return
	}
}

// 房间帧循环
func (room *Room) frameLoop(server *Server) {
	ticker := time.NewTicker(FRAME_INTERVAL)
	defer ticker.Stop()

	fmt.Printf("Frame loop started for room %s\n", room.ID)

	for range ticker.C {
		room.Mutex.Lock()
		frameDatas := room.FrameDataBuffer
		room.FrameDataBuffer = make([]*myproto.FrameData, 0)
		room.FrameNumber++
		clients := make([]*Client, 0, len(room.Clients))
		for _, c := range room.Clients {
			clients = append(clients, c)
		}
		clientCount := len(clients)
		room.Mutex.Unlock()

		// 如果房间没有客户端，停止帧循环
		if clientCount == 0 {
			fmt.Printf("Room %s has no clients, stopping frame loop\n", room.ID)
			return
		}

		// 构建服务器帧数据
		serverFrame := &myproto.ServerFrame{
			FrameNumber: room.FrameNumber,
			Timestamp:   time.Now().UnixNano(),
			FrameDatas:  frameDatas,
		}

		// 保存到历史记录
		room.Mutex.Lock()
		room.HistoryFrames = append(room.HistoryFrames, serverFrame)
		room.Mutex.Unlock()

		// 发送给所有客户端
		for _, client := range clients {
			server.sendMessage(client.Conn, myproto.MessageType_MESSAGE_SERVER_FRAME, serverFrame)
		}
	}
}

// 定期清理空房间
func (s *Server) cleanupEmptyRooms() {
	ticker := time.NewTicker(30 * time.Second)
	defer ticker.Stop()

	for range ticker.C {
		s.Mutex.Lock()
		roomsToDelete := make([]string, 0)

		for roomID, room := range s.Rooms {
			room.Mutex.Lock()
			if len(room.Clients) == 0 {
				roomsToDelete = append(roomsToDelete, roomID)
			}
			room.Mutex.Unlock()
		}

		// 删除空房间
		for _, roomID := range roomsToDelete {
			delete(s.Rooms, roomID)
			fmt.Printf("Room %s deleted by cleanup task\n", roomID)
		}
		s.Mutex.Unlock()
	}
}

func main() {
	server := NewServer()
	// 同时启动TCP和KCP服务器（兼容旧客户端和新客户端）
	server.StartBoth()
}
