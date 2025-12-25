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
	FRAME_INTERVAL = 50 * time.Millisecond // 20帧每秒
	PORT           = ":8088"
	MAX_PLAYERS    = 1 // 每个房间最大玩家数
)

// 全局客户端计数器
var clientCounter int64 = 0

// 客户端结构
type Client struct {
	ID       string
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
	HostID          string
	Clients         map[string]*Client
	FrameDataBuffer []*myproto.FrameData // 帧数据缓冲区
	FrameNumber     int64
	Status          string // "waiting", "playing"
	MaxPlayers      int32
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

	clientID := strconv.FormatInt(clientCounter, 10)
	clientCounter++
	client := &Client{
		ID:       clientID,
		Conn:     conn,
		LastSeen: time.Now(),
	}

	fmt.Printf("Client %s connected\n", client.ID)

	// 发送连接成功消息
	connectMsg := &myproto.ConnectMessage{
		PlayerId:   clientID,
		PlayerName: "",
	}
	s.sendMessage(conn, myproto.MessageType_MESSAGE_CONNECT, connectMsg)

	reader := bufio.NewReader(conn)
	for {
		// 读取消息长度 (4 bytes)
		lengthBytes := make([]byte, 4)
		_, err := reader.Read(lengthBytes)
		if err != nil {
			log.Printf("Client %s: Read length error: %v\n", client.ID, err)
			break
		}
		length := binary.BigEndian.Uint32(lengthBytes)

		// 读取消息类型 (1 byte)
		messageTypeBytes := make([]byte, 1)
		_, err = reader.Read(messageTypeBytes)
		if err != nil {
			log.Printf("Client %s: Read message type error: %v\n", client.ID, err)
			break
		}
		messageType := myproto.MessageType(messageTypeBytes[0])

		// 读取数据部分 (length - 1 byte for messageType)
		dataLength := int(length) - 1
		if dataLength < 0 {
			log.Printf("Client %s: Invalid message length\n", client.ID)
			break
		}
		data := make([]byte, dataLength)
		_, err = reader.Read(data)
		if err != nil {
			log.Printf("Client %s: Read data error: %v\n", client.ID, err)
			break
		}

		// 更新最后活跃时间
		client.LastSeen = time.Now()

		// 根据消息类型处理
		switch messageType {
		case myproto.MessageType_MESSAGE_CONNECT:
			s.handleConnect(client, data)
		case myproto.MessageType_MESSAGE_FRAME_DATA:
			s.handleFrameData(client, data)
		case myproto.MessageType_MESSAGE_DISCONNECT:
			s.handleDisconnect(client, data)
		default:
			log.Printf("Client %s: Unknown message type: %d\n", client.ID, messageType)
		}
	}

	// 客户端断开连接
	s.handleClientDisconnect(client)
}

// 处理连接消息
func (s *Server) handleConnect(client *Client, data []byte) {
	var connectMsg myproto.ConnectMessage
	if err := proto.Unmarshal(data, &connectMsg); err != nil {
		log.Printf("Client %s: Unmarshal connect message error: %v\n", client.ID, err)
		return
	}

	client.Name = connectMsg.PlayerName
	if connectMsg.PlayerId != "" {
		client.ID = connectMsg.PlayerId
	}
	fmt.Printf("Client %s connected with name: %s\n", client.ID, client.Name)

	// 自动分配房间：查找等待中的房间或创建新房间
	s.autoAssignRoom(client)
}

// 处理帧数据
func (s *Server) handleFrameData(client *Client, data []byte) {
	if client.RoomID == "" {
		fmt.Printf("Client %s no room  %s\n", client.ID, client.Name)
		return
	}

	s.Mutex.Lock()
	room, exists := s.Rooms[client.RoomID]
	s.Mutex.Unlock()

	if !exists {
		log.Printf("Client %s: Room not found: %s\n", client.ID, client.RoomID)
		return
	}

	room.Mutex.Lock()
	// 只有游戏开始后才能接收帧数据
	if room.Status != "playing" {
		room.Mutex.Unlock()
		fmt.Printf("Client %s: Game not started yet, ignoring frame data\n", client.ID)
		return
	}
	room.Mutex.Unlock()

	var frameData myproto.FrameData
	if err := proto.Unmarshal(data, &frameData); err != nil {
		log.Printf("Client %s: Unmarshal frame data error: %v\n", client.ID, err)
		return
	}

	// 确保player_id正确
	if frameData.PlayerId == "" {
		frameData.PlayerId = client.ID
	}

	room.Mutex.Lock()
	// 将客户端的帧数据添加到房间的缓冲区
	log.Printf("Client %s: frame data\n", client.ID)

	room.FrameDataBuffer = append(room.FrameDataBuffer, &frameData)
	room.Mutex.Unlock()
}

// 处理断开连接消息
func (s *Server) handleDisconnect(client *Client, data []byte) {
	fmt.Printf("Client %s requested disconnect\n", client.ID)
	s.handleClientDisconnect(client)
}

// 处理客户端断开
func (s *Server) handleClientDisconnect(client *Client) {
	fmt.Printf("Client %s disconnected\n", client.ID)

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
			fmt.Printf("New host selected: %s in room %s\n", c.ID, room.ID)
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
	fmt.Printf("Client %s disconnected from room %s, %d players remaining\n", client.ID, room.ID, len(room.Clients))
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
		Clients:         make(map[string]*Client),
		FrameDataBuffer: make([]*myproto.FrameData, 0),
		Status:          "waiting",
		MaxPlayers:      maxPlayers,
	}

	s.Mutex.Lock()
	s.Rooms[roomID] = room
	s.Mutex.Unlock()

	// 将客户端加入房间
	client.RoomID = roomID
	client.IsHost = true
	room.Clients[client.ID] = client

	fmt.Printf("Client %s created room %s (%s)\n", client.ID, roomID, roomName)
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

	fmt.Printf("Client %s joined room %s (%d/%d players)\n", client.ID, roomID, len(room.Clients), room.MaxPlayers)

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
	s.Mutex.Lock()
	defer s.Mutex.Unlock()

	// 查找等待中的房间
	for _, room := range s.Rooms {
		room.Mutex.Lock()
		if room.Status == "waiting" && int32(len(room.Clients)) < room.MaxPlayers {
			room.Mutex.Unlock()
			// 找到可用房间，加入
			if s.JoinRoom(client, room.ID) {
				return
			}
		} else {
			room.Mutex.Unlock()
		}
	}

	// 没有找到可用房间，创建新房间
	roomID := strconv.FormatInt(int64(len(s.Rooms)+1), 10)
	roomName := fmt.Sprintf("Room %s", roomID)

	room := &Room{
		ID:              roomID,
		Name:            roomName,
		HostID:          client.ID,
		Clients:         make(map[string]*Client),
		FrameDataBuffer: make([]*myproto.FrameData, 0),
		Status:          "waiting",
		MaxPlayers:      MAX_PLAYERS,
	}

	s.Rooms[roomID] = room

	// 将客户端加入房间
	client.RoomID = roomID
	client.IsHost = true
	room.Clients[client.ID] = client

	fmt.Printf("Client %s created room %s (%s) (%d/%d players)\n", client.ID, roomID, roomName, len(room.Clients), room.MaxPlayers)

	// 如果房间人数达到上限（包括测试情况：1人时也开始游戏），自动开始游戏
	if int32(len(room.Clients)) >= room.MaxPlayers {
		fmt.Printf("Room %s reached max players (%d/%d), starting game...\n", roomID, len(room.Clients), room.MaxPlayers)
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
	playerIDs := make([]string, 0, len(room.Clients))
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
	conn.Write(lengthBytes)

	// 写入消息类型 (1 byte)
	conn.Write([]byte{byte(messageType)})

	// 写入数据
	conn.Write(data)
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
	server.Start()
}
