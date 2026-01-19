package main

import (
	"encoding/binary"
	"fmt"
	"net"
	"time"

	myproto "github.com/WjcHome/gohello/proto"
	"google.golang.org/protobuf/proto"
)

func main() {
	// 创建UDP连接
	conn, err := net.Dial("udp", "127.0.0.1:8888")
	if err != nil {
		fmt.Printf("Failed to connect to UDP server: %v\n", err)
		return
	}
	defer conn.Close()

	fmt.Println("Connected to UDP server at 127.0.0.1:8091")

	// 创建ConnectMessage
	connectMsg := &myproto.ConnectMessage{
		PlayerId:   0,
		PlayerName: "TestClient",
	}

	// 序列化消息
	data, err := proto.Marshal(connectMsg)
	if err != nil {
		fmt.Printf("Failed to marshal message: %v\n", err)
		return
	}

	// 计算总长度：1 byte (messageType) + data length
	totalLength := uint32(1 + len(data))

	// 写入长度 (4 bytes, big endian)
	lengthBytes := make([]byte, 4)
	binary.BigEndian.PutUint32(lengthBytes, totalLength)

	// 打包消息类型（1字节）
	messageType := byte(myproto.MessageType_MESSAGE_CONNECT)

	// 合并所有数据：length(4) + type(1) + data(n)
	sendBuffer := make([]byte, 4+1+len(data))
	copy(sendBuffer[0:4], lengthBytes)
	sendBuffer[4] = messageType
	copy(sendBuffer[5:], data)

	fmt.Printf("Sending UDP message:\n")
	fmt.Printf("  Total length: %d\n", totalLength)
	fmt.Printf("  Message type: %d (MESSAGE_CONNECT)\n", messageType)
	fmt.Printf("  Data length: %d\n", len(data))
	fmt.Printf("  Buffer content: %x\n", sendBuffer)

	// 发送消息
	_, err = conn.Write(sendBuffer)
	if err != nil {
		fmt.Printf("Failed to send UDP message: %v\n", err)
		return
	}

	fmt.Println("UDP message sent successfully")

	// 等待一段时间看是否有响应
	fmt.Println("Waiting for response...")
	conn.SetReadDeadline(time.Now().Add(5 * time.Second))

	buffer := make([]byte, 1024)
	n, err := conn.Read(buffer)
	if err != nil {
		fmt.Printf("No response received: %v\n", err)
	} else {
		fmt.Printf("Received response: %d bytes\n", n)
		fmt.Printf("Response data: %x\n", buffer[:n])
	}
}
