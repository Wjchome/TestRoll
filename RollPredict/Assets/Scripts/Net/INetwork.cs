using System;
using Proto; // 引入你的 Protobuf 生成的类（ServerFrame、GameStart 等）

/// <summary>
/// 帧同步网络核心接口（一站式模板，直接复用）
/// </summary>
public interface INetwork
{
    #region 核心属性（可选，按需添加）
    /// <summary>
    /// 是否已连接（业务层状态）
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// 是否游戏已开始
    /// </summary>
    bool IsGameStarted { get; }
    int MyID { get; }
    

    #endregion

    #region 核心方法（UDP/TCP 通用）
    /// <summary>
    /// 连接服务器
    /// </summary>
    void Connect();

    
    /// <summary>
    /// 发送帧数据
    /// </summary>
    void SendFrameData(InputDirection direction, bool isFire = false, long fireX = 0, long fireY = 0, bool isToggle = false);

    /// <summary>
    /// 发送帧丢失补发请求
    /// </summary>
    void SendLossFrame(long confirmedFrame);


    #endregion

    #region 事件回调（你需要的核心部分）
    /// <summary>
    /// 连接成功回调（参数：玩家ID）
    /// </summary>
    event Action<long> OnConnected;

    /// <summary>
    /// 断开连接回调
    /// </summary>
    event Action OnDisconnected;

    /// <summary>
    /// 游戏开始回调（参数：GameStart 协议）
    /// </summary>
    event Action<GameStart> OnGameStarted;

    /// <summary>
    /// 收到服务器帧数据回调（参数：ServerFrame 协议）
    /// </summary>
    event Action<ServerFrame> OnServerFrameReceived;
    #endregion
}