using System;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.QQLib;

namespace PlugBotSharp.QQProxy;

/// <summary>
/// 发消息的会话类型
/// </summary>
public enum MessageScene
{
    /// <summary>
    /// 群聊
    /// </summary>
    Group,
    /// <summary>
    /// 单聊
    /// </summary>
    User
}

public interface IProxy
{
    /// <summary>
    /// 设置访问凭证
    /// </summary>
    public void SetToken(string? token);

    /// <summary>
    /// 停止代理
    /// </summary>
    public void Stop();

    /// <summary>
    /// 发送消息
    /// </summary>
    /// <param name="target">群 openid 或用户 openid</param>
    /// <param name="scene">会话类型</param>
    /// <param name="message">消息</param>
    /// <param name="replyId">被动回复的消息 id</param>
    /// <param name="sequence">回复序号</param>
    public Task<SendGroupMessageResObj?> Send(string target, MessageScene scene, BaseMessage message, string? replyId, int sequence);
}
