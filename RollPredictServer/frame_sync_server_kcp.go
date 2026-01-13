package main

import (
	"bufio"
	"encoding/binary"
	"fmt"
	"log"
	"net"
	"time"

	myproto "github.com/WjcHome/gohello/proto"
	"github.com/xtaci/kcp-go/v5"
	"google.golang.org/protobuf/proto"
)

const (
	KCP_PORT = ":8088" // KCP服务器端口
	TCP_PORT = ":8089" // TCP服务器端口（可选，用于兼容）
)

// KCP配置
func configureKCP(conn *kcp.UDPSession) {
	// 快速模式配置（低延迟，适合帧同步）
	conn.SetNoDelay(1, 10, 2, 1) // nodelay, interval, resend, nc
	conn.SetWindowSize(128, 128) // send window, recv window
	conn.SetMtu(1400)            // MTU
	conn.SetACKNoDelay(true)     // 立即发送ACK
	conn.SetStreamMode(false)    // 非流模式（数据包模式）
}

// 启动KCP服务器
func (s *Server) StartKCP() {
	// 启动定期清理任务
	go s.cleanupEmptyRooms()
	// 启动心跳超时检测
	go s.checkHeartbeatTimeout()

	// 监听UDP端口（使用ListenWithOptions获取*Listener类型，支持AcceptKCP）
	// 参数：laddr, block(加密，nil表示不加密), dataShards, parityShards(前向纠错，0表示不使用)
	ln, err := kcp.ListenWithOptions(KCP_PORT, nil, 0, 0)
	if err != nil {
		log.Fatal("KCP Listen error:", err)
	}
	defer ln.Close()

	fmt.Printf("KCP Frame Sync Server started on %s\n", KCP_PORT)

	for {
		conn, err := ln.AcceptKCP()
		if err != nil {
			log.Println("AcceptKCP error:", err)
			continue
		}

		// 配置KCP参数
		configureKCP(conn)

		go s.handleKCPClient(conn)
	}
}

// 处理KCP客户端连接
func (s *Server) handleKCPClient(conn *kcp.UDPSession) {
	defer func() {
		// 优雅关闭KCP连接
		conn.Close()
	}()

	// KCP连接不需要初始设置读取超时，在循环中动态设置

	clientID := int32(clientCounter)
	clientCounter++
	client := &Client{
		ID:       clientID,
		Conn:     conn,
		LastSeen: time.Now(),
	}

	fmt.Printf("KCP Client %d connected from %s\n", client.ID, conn.RemoteAddr())

	// 发送连接成功消息
	connectMsg := &myproto.ConnectMessage{
		PlayerId:   clientID,
		PlayerName: "",
	}
	s.sendKCPMessage(conn, myproto.MessageType_MESSAGE_CONNECT, connectMsg)

	s.autoAssignRoom(client)

	reader := bufio.NewReader(conn)
	for {
		// 设置读取超时（30秒，避免长时间阻塞）
		// 超时后不会断开连接，只是跳过本次读取，继续等待下次消息
		conn.SetReadDeadline(time.Now().Add(30 * time.Second))

		// 读取消息长度 (4 bytes)
		lengthBytes := make([]byte, 4)
		_, err := reader.Read(lengthBytes)
		if err != nil {
			// 检查是否是超时错误（可以继续等待）
			if netErr, ok := err.(net.Error); ok && netErr.Timeout() {
				// 超时不是致命错误，继续循环等待
				// KCP连接可能暂时没有数据，但连接仍然有效
				log.Printf("KCP Client %d: Read timeout, continuing...\n", client.ID)
				continue
			}
			// 其他错误（如EOF、连接关闭）才断开
			log.Printf("KCP Client %d: Read length error: %v\n", client.ID, err)
			break
		}
		length := binary.BigEndian.Uint32(lengthBytes)

		// 验证消息长度（防止恶意或错误数据）
		if length > 1024*1024 { // 最大1MB
			log.Printf("KCP Client %d: Message too large: %d bytes\n", client.ID, length)
			break
		}

		// 读取消息类型 (1 byte)
		messageTypeBytes := make([]byte, 1)
		_, err = reader.Read(messageTypeBytes)
		if err != nil {
			if netErr, ok := err.(net.Error); ok && netErr.Timeout() {
				log.Printf("KCP Client %d: Read message type timeout, continuing...\n", client.ID)
				continue
			}
			log.Printf("KCP Client %d: Read message type error: %v\n", client.ID, err)
			break
		}
		messageType := myproto.MessageType(messageTypeBytes[0])

		// 读取数据部分 (length - 1 byte for messageType)
		dataLength := int(length) - 1
		if dataLength < 0 {
			log.Printf("KCP Client %d: Invalid message length: %d\n", client.ID, length)
			break
		}
		data := make([]byte, dataLength)
		_, err = reader.Read(data)
		if err != nil {
			if netErr, ok := err.(net.Error); ok && netErr.Timeout() {
				log.Printf("KCP Client %d: Read data timeout, continuing...\n", client.ID)
				continue
			}
			log.Printf("KCP Client %d: Read data error: %v\n", client.ID, err)
			break
		}

		// 更新最后活跃时间（任何消息都会更新心跳时间，包括帧数据、心跳、丢帧请求等）
		client.LastSeen = time.Now()

		// 根据消息类型处理
		switch messageType {
		case myproto.MessageType_MESSAGE_CONNECT:
			// 客户端发送的ConnectMessage用于触发KCP连接建立，服务器端已经发送了ConnectMessage响应
			// 这里可以记录或忽略
			log.Printf("KCP Client %d: Received connect message (already connected)\n", client.ID)
		case myproto.MessageType_MESSAGE_FRAME_DATA:
			s.handleFrameData(client, data)
		case myproto.MessageType_MESSAGE_DISCONNECT:
			s.handleDisconnect(client, data)
		case myproto.MessageType_MESSAGE_FRAME_LOSS:
			s.handleFrameLoss(client, data)
		case myproto.MessageType_MESSAGE_HEARTBEAT:
			// 心跳消息，LastSeen 已经在上面更新，这里不需要额外操作
		default:
			log.Printf("KCP Client %d: Unknown message type: %d\n", client.ID, messageType)
		}
	}

	// 客户端断开连接
	s.handleClientDisconnect(client)
}

// 发送KCP消息
func (s *Server) sendKCPMessage(conn *kcp.UDPSession, messageType myproto.MessageType, msg proto.Message) {
	data, err := proto.Marshal(msg)
	if err != nil {
		log.Printf("KCP Marshal error: %v\n", err)
		return
	}

	// 消息格式：len(4 bytes) + messageType(1 byte) + data
	// 重要：必须一次性写入所有数据，避免KCP将消息分片
	totalLength := uint32(1 + len(data))
	lengthBytes := make([]byte, 4)
	binary.BigEndian.PutUint32(lengthBytes, totalLength)

	// 组合完整消息到一个缓冲区
	message := make([]byte, 4+1+len(data))
	copy(message[0:4], lengthBytes)
	message[4] = byte(messageType)
	copy(message[5:], data)

	// 一次性写入完整消息
	_, err = conn.Write(message)
	if err != nil {
		log.Printf("KCP Write error: %v\n", err)
		return
	}
}

// 同时支持TCP和KCP的服务器启动函数
func (s *Server) StartBoth() {
	// 启动定期清理任务
	go s.cleanupEmptyRooms()
	// 启动心跳超时检测（只需要启动一次）
	go s.checkHeartbeatTimeout()

	// 启动TCP服务器（兼容旧客户端）
	go func() {
		ln, err := net.Listen("tcp", TCP_PORT)
		if err != nil {
			log.Printf("TCP Listen error: %v\n", err)
			return
		}
		defer ln.Close()
		fmt.Printf("TCP Frame Sync Server started on %s\n", TCP_PORT)

		for {
			conn, err := ln.Accept()
			if err != nil {
				log.Println("TCP Accept error:", err)
				continue
			}
			go s.handleClient(conn)
		}
	}()

	// 启动KCP服务器
	s.StartKCP()
}
