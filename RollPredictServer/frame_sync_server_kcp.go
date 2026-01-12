package main

import (
	"bufio"
	"encoding/binary"
	"fmt"
	"log"
	"net"
	"sync"
	"time"

	myproto "github.com/WjcHome/gohello/proto"
	"github.com/xtaci/kcp-go/v5"
	"google.golang.org/protobuf/proto"
)

const (
	FRAME_INTERVAL = 200 * time.Millisecond // 20帧每秒
	KCP_PORT       = ":8088"                // KCP服务器端口
	TCP_PORT       = ":8089"                // TCP服务器端口（可选，用于兼容）
	MAX_PLAYERS    = 2                      // 每个房间最大玩家数
)

// KCP配置
func configureKCP(conn *kcp.UDPSession) {
	// 快速模式配置（低延迟，适合帧同步）
	conn.SetNoDelay(1, 10, 2, 1) // nodelay, interval, resend, nc
	conn.SetWindowSize(128, 128)  // send window, recv window
	conn.SetMtu(1400)             // MTU
	conn.SetACKNoDelay(true)      // 立即发送ACK
	conn.SetStreamMode(false)     // 非流模式（数据包模式）
}

// 启动KCP服务器
func (s *Server) StartKCP() {
	// 启动定期清理任务
	go s.cleanupEmptyRooms()

	// 监听UDP端口
	ln, err := kcp.Listen(KCP_PORT)
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
	defer conn.Close()

	// 设置读取超时（30秒无数据则断开）
	conn.SetReadDeadline(time.Now().Add(30 * time.Second))

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
		// 更新读取超时
		conn.SetReadDeadline(time.Now().Add(30 * time.Second))

		// 读取消息长度 (4 bytes)
		lengthBytes := make([]byte, 4)
		_, err := reader.Read(lengthBytes)
		if err != nil {
			log.Printf("KCP Client %d: Read length error: %v\n", client.ID, err)
			break
		}
		length := binary.BigEndian.Uint32(lengthBytes)

		// 读取消息类型 (1 byte)
		messageTypeBytes := make([]byte, 1)
		_, err = reader.Read(messageTypeBytes)
		if err != nil {
			log.Printf("KCP Client %d: Read message type error: %v\n", client.ID, err)
			break
		}
		messageType := myproto.MessageType(messageTypeBytes[0])

		// 读取数据部分 (length - 1 byte for messageType)
		dataLength := int(length) - 1
		if dataLength < 0 {
			log.Printf("KCP Client %d: Invalid message length\n", client.ID)
			break
		}
		data := make([]byte, dataLength)
		_, err = reader.Read(data)
		if err != nil {
			log.Printf("KCP Client %d: Read data error: %v\n", client.ID, err)
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
	totalLength := uint32(1 + len(data))
	lengthBytes := make([]byte, 4)
	binary.BigEndian.PutUint32(lengthBytes, totalLength)

	// 写入长度
	_, err = conn.Write(lengthBytes)
	if err != nil {
		log.Printf("KCP Write length error: %v\n", err)
		return
	}

	// 写入消息类型
	_, err = conn.Write([]byte{byte(messageType)})
	if err != nil {
		log.Printf("KCP Write message type error: %v\n", err)
		return
	}

	// 写入数据
	_, err = conn.Write(data)
	if err != nil {
		log.Printf("KCP Write data error: %v\n", err)
		return
	}
}

// 同时支持TCP和KCP的服务器启动函数
func (s *Server) StartBoth() {
	// 启动定期清理任务
	go s.cleanupEmptyRooms()

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

