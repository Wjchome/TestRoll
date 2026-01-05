package main

import (
	"flag"
	"fmt"
	"io"
	"log"
	"math/rand"
	"net"
	"time"
)

var (
	// 代理监听端口（客户端连接这里）
	listenPort = flag.String("listen", "8089", "代理监听端口（客户端连接此端口）")
	// 实际服务器地址（代理转发到这里）
	targetHost = flag.String("target", "127.0.0.1:8088", "实际服务器地址（代理转发到此）")
	delay      = flag.Int("delay", 0, "延迟（毫秒）")
	loss       = flag.Float64("loss", 0, "丢包率（0-100）")
)

func main() {
	flag.Parse()

	rand.Seed(time.Now().UnixNano())

	listener, err := net.Listen("tcp", ":"+*listenPort)
	if err != nil {
		log.Fatal("监听失败:", err)
	}

	fmt.Printf("========================================\n")
	fmt.Printf("网络模拟器代理启动\n")
	fmt.Printf("========================================\n")
	fmt.Printf("代理监听端口: %s (客户端连接这里)\n", *listenPort)
	fmt.Printf("转发到服务器: %s (实际服务器地址)\n", *targetHost)
	fmt.Printf("单向延迟: %dms (往返延迟: %dms)\n", *delay, *delay*2)
	if *loss > 0 {
		fmt.Printf("丢包率: %.2f%%\n", *loss)
	}
	fmt.Printf("========================================\n")
	fmt.Printf("注意：如果客户端使用了预测回滚机制，\n")
	fmt.Printf("延迟可能被掩盖，但实际延迟仍然存在！\n")
	fmt.Printf("========================================\n")
	fmt.Printf("使用说明：\n")
	fmt.Printf("1. 先启动实际服务器在 %s\n", *targetHost)
	fmt.Printf("2. 然后启动此代理\n")
	fmt.Printf("3. 客户端连接到 %s (代理端口)\n", *listenPort)
	fmt.Printf("========================================\n")
	fmt.Printf("按 Ctrl+C 停止\n\n")

	for {
		clientConn, err := listener.Accept()
		if err != nil {
			log.Println("接受连接失败:", err)
			continue
		}

		go handleConnection(clientConn)
	}
}

func handleConnection(clientConn net.Conn) {
	defer clientConn.Close()

	// 连接到目标服务器
	serverConn, err := net.Dial("tcp", *targetHost)
	if err != nil {
		log.Printf("连接目标服务器失败 (%s): %v\n", *targetHost, err)
		return
	}
	defer serverConn.Close()

	log.Printf("新连接: %s -> %s (通过代理)\n", clientConn.RemoteAddr(), *targetHost)

	// 双向转发数据
	done := make(chan bool, 2)

	// 客户端 -> 服务器
	go func() {
		defer func() { done <- true }()
		copyWithDelay(clientConn, serverConn, "客户端->服务器")
	}()

	// 服务器 -> 客户端
	go func() {
		defer func() { done <- true }()
		copyWithDelay(serverConn, clientConn, "服务器->客户端")
	}()

	<-done
	log.Printf("连接关闭: %s\n", clientConn.RemoteAddr())
}

func copyWithDelay(src, dst net.Conn, direction string) {
	buffer := make([]byte, 4096)

	for {
		n, err := src.Read(buffer)
		if err != nil {
			if err != io.EOF {
				log.Printf("[%s] 读取错误: %v\n", direction, err)
			}
			return
		}

		if n == 0 {
			continue
		}

		// 检查是否丢包
		if *loss > 0 {
			if rand.Float64()*100 < *loss {
				log.Printf("[%s] 丢包: %d 字节\n", direction, n)
				continue
			}
		}

		// 应用延迟
		if *delay > 0 {
			before := time.Now()
			time.Sleep(time.Duration(*delay) * time.Millisecond)
			actualDelay := time.Since(before)
			// 记录每个数据包的延迟（前10个包详细记录，之后每10个包记录一次）
			log.Printf("[%s] 数据包: %d 字节, 应用延迟: %v (目标: %dms)\n",
				direction, n, actualDelay, *delay)
		} else {
			// 即使没有延迟，也记录数据包传输（用于调试）
			log.Printf("[%s] 数据包: %d 字节 (无延迟)\n", direction, n)
		}

		// 写入数据
		_, err = dst.Write(buffer[:n])
		if err != nil {
			log.Printf("[%s] 写入错误: %v\n", direction, err)
			return
		}
	}
}
