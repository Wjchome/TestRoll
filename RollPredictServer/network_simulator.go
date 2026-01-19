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
	listenPort = flag.String("listen", "9999", "代理监听端口（客户端连接此端口）")
	// 实际服务器地址（代理转发到这里）
	targetHost = flag.String("target", "127.0.0.1:8888", "实际服务器地址（代理转发到此）")
	delay      = flag.Int("delay", 0, "延迟（毫秒）")
	loss       = flag.Float64("loss", 0, "丢包率（0-100）")
	protocol   = flag.String("protocol", "udp", "协议类型：tcp 或 udp")
)

func main() {
	flag.Parse()

	rand.Seed(time.Now().UnixNano())

	var listener net.Listener
	var udpConn *net.UDPConn

	if *protocol == "udp" {
		// UDP需要使用不同的监听方式
		udpAddr, err := net.ResolveUDPAddr("udp", ":"+*listenPort)
		if err != nil {
			log.Fatal("解析UDP地址失败:", err)
		}
		udpConn, err = net.ListenUDP("udp", udpAddr)
		if err != nil {
			log.Fatal("UDP监听失败:", err)
		}
	} else {
		// TCP使用普通的Listen
		var err error
		listener, err = net.Listen("tcp", ":"+*listenPort)
		if err != nil {
			log.Fatal("TCP监听失败:", err)
		}
	}

	fmt.Printf("========================================\n")
	fmt.Printf("网络模拟器代理启动 (%s)\n", *protocol)
	fmt.Printf("========================================\n")
	fmt.Printf("代理监听端口: %s (客户端连接这里)\n", *listenPort)
	fmt.Printf("转发到服务器: %s (实际服务器地址)\n", *targetHost)
	fmt.Printf("协议: %s\n", *protocol)
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
	fmt.Printf("UDP说明：UDP模式下，代理作为中间人转发数据包\n")
	fmt.Printf("每个UDP数据包都会被单独处理和延迟\n")
	fmt.Printf("========================================\n")
	fmt.Printf("测试UDP延迟示例：\n")
	fmt.Printf("./network_simulator -protocol=udp -listen=9999 -target=127.0.0.1:8888 -delay=100 -loss=5\n")
	fmt.Printf("========================================\n")
	fmt.Printf("按 Ctrl+C 停止\n\n")

	if *protocol == "udp" {
		// UDP直接处理连接（阻塞）
		handleUDPConnection(udpConn)
	} else {
		// TCP接受连接
		for {
			clientConn, err := listener.Accept()
			if err != nil {
				log.Println("接受连接失败:", err)
				continue
			}

			go handleConnection(clientConn)
		}
	}
}

func handleConnection(clientConn net.Conn) {
	defer clientConn.Close()

	if *protocol == "udp" {
		handleUDPConnection(clientConn.(*net.UDPConn))
		return
	}

	// TCP处理（原有逻辑）
	// 连接到目标服务器
	serverConn, err := net.Dial("tcp", *targetHost)
	if err != nil {
		log.Printf("连接目标服务器失败 (%s): %v\n", *targetHost, err)
		return
	}
	defer serverConn.Close()

	log.Printf("新TCP连接: %s -> %s (通过代理)\n", clientConn.RemoteAddr(), *targetHost)

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
	log.Printf("TCP连接关闭: %s\n", clientConn.RemoteAddr())
}

func handleUDPConnection(clientConn *net.UDPConn) {
	// 解析目标服务器地址
	serverAddr, err := net.ResolveUDPAddr("udp", *targetHost)
	if err != nil {
		log.Printf("解析目标服务器地址失败 (%s): %v\n", *targetHost, err)
		return
	}

	log.Printf("UDP代理启动: %s <-> %s\n", clientConn.LocalAddr(), *targetHost)

	buffer := make([]byte, 4096)

	for {
		// 从客户端接收数据
		n, clientAddr, err := clientConn.ReadFromUDP(buffer)
		if err != nil {
			log.Printf("UDP读取客户端数据错误: %v\n", err)
			return
		}

		if n == 0 {
			continue
		}

		// 检查是否丢包
		if *loss > 0 {
			if rand.Float64()*100 < *loss {
				log.Printf("[UDP客户端->服务器] 丢包: %d 字节 from %s\n", n, clientAddr.String())
				continue
			}
		}

		// 应用延迟
		if *delay > 0 {
			before := time.Now()
			time.Sleep(time.Duration(*delay) * time.Millisecond)
			actualDelay := time.Since(before)
			log.Printf("[UDP客户端->服务器] 数据包: %d 字节 from %s, 应用延迟: %v (目标: %dms)\n",
				n, clientAddr.String(), actualDelay, *delay)
		} else {
			log.Printf("[UDP客户端->服务器] 数据包: %d 字节 from %s (无延迟)\n", n, clientAddr.String())
		}

		// 转发到服务器
		_, err = clientConn.WriteToUDP(buffer[:n], serverAddr)
		if err != nil {
			log.Printf("UDP转发到服务器错误: %v\n", err)
			return
		}

		// 尝试接收服务器响应并转发回客户端
		clientConn.SetReadDeadline(time.Now().Add(100 * time.Millisecond))
		n, _, err = clientConn.ReadFromUDP(buffer)
		if err != nil {
			// 超时是正常的，继续等待下一个客户端请求
			continue
		}

		// 检查是否丢包（服务器->客户端）
		if *loss > 0 {
			if rand.Float64()*100 < *loss {
				log.Printf("[UDP服务器->客户端] 丢包: %d 字节\n", n)
				continue
			}
		}

		// 应用延迟（服务器->客户端）
		if *delay > 0 {
			before := time.Now()
			time.Sleep(time.Duration(*delay) * time.Millisecond)
			actualDelay := time.Since(before)
			log.Printf("[UDP服务器->客户端] 数据包: %d 字节, 应用延迟: %v (目标: %dms)\n",
				n, actualDelay, *delay)
		} else {
			log.Printf("[UDP服务器->客户端] 数据包: %d 字节 (无延迟)\n", n)
		}

		// 转发回客户端
		_, err = clientConn.WriteToUDP(buffer[:n], clientAddr)
		if err != nil {
			log.Printf("UDP转发到客户端错误: %v\n", err)
			return
		}
	}
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
			log.Printf("[%s] 数据包: %d 字节, 应用延迟: %v (目标: %dms)\n",
				direction, n, actualDelay, *delay)
		} else {
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
