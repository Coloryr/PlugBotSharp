namespace PlugBotSharp.Core.Messages;

/// <summary>
/// 消息基类，代理按 Type 分派
/// </summary>
public abstract record BaseMessage
{
    /// <summary>
    /// 消息类型
    /// </summary>
    public MessageType Type { get; init; }
}
