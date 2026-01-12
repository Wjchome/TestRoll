#!/bin/bash

# 启动KCP服务器脚本

echo "正在编译服务器..."
go build -o frame_sync_server frame_sync_server.go frame_sync_server_kcp.go

if [ $? -eq 0 ]; then
    echo "编译成功！"
    echo "启动服务器（同时支持TCP和KCP）..."
    echo "TCP服务器监听: :8089"
    echo "KCP服务器监听: :8088"
    echo ""
    ./frame_sync_server
else
    echo "编译失败！"
    exit 1
fi

