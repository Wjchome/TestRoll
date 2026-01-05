#!/bin/bash

# 测试代理是否工作的脚本

echo "=========================================="
echo "网络代理测试脚本"
echo "=========================================="
echo ""

# 检查服务器是否在运行
echo "1. 检查服务器是否在 8088 端口运行..."
if lsof -Pi :8088 -sTCP:LISTEN -t >/dev/null ; then
    echo "   ✅ 服务器正在 8088 端口运行"
else
    echo "   ❌ 服务器未在 8088 端口运行"
    echo "   请先启动: ./frame_sync_server"
    exit 1
fi

# 检查代理是否在运行
echo ""
echo "2. 检查代理是否在 8089 端口运行..."
if lsof -Pi :8089 -sTCP:LISTEN -t >/dev/null ; then
    echo "   ✅ 代理正在 8089 端口运行"
else
    echo "   ❌ 代理未在 8089 端口运行"
    echo "   请先启动: ./network_simulator -delay 100"
    exit 1
fi

# 测试连接
echo ""
echo "3. 测试代理连接..."
timeout 2 nc -zv 127.0.0.1 8089 2>&1
if [ $? -eq 0 ]; then
    echo "   ✅ 可以连接到代理端口 8089"
else
    echo "   ❌ 无法连接到代理端口 8089"
fi

echo ""
echo "=========================================="
echo "检查 Unity 客户端配置："
echo "=========================================="
echo "请确认 Unity 客户端中的连接地址是："
echo "  serverIP = \"127.0.0.1\""
echo "  serverPort = 8089  <-- 必须是 8089（代理端口）"
echo ""
echo "如果还是 8088，请修改 FrameSyncNetwork.cs 中的 serverPort"
echo "=========================================="


