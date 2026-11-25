namespace PlugBotSharp.Core.Messages;

/// <summary>
/// 代理上报的消息事件
/// </summary>
public record ProxyMessage
{
    /// <summary>
    /// 代理组件 id
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// 实例 uuid
    /// </summary>
    public required Guid Uuid { get; init; }
    /// <summary>
    /// 发送者类型
    /// </summary>
    public required MessageSenderType SenderType { get; init; }
    /// <summary>
    /// 发送者
    /// </summary>
    public required string Sender { get; set; }
    /// <summary>
    /// 回复目标，群号 / 频道号 / 用户 openid，为空时用 Sender
    /// </summary>
    public string Target { get; init; } = "";
    /// <summary>
    /// 消息
    /// </summary>
    public required BaseMessage Message { get; init; }
    /// <summary>
    /// 平台消息 id，被动回复时带上
    /// </summary>
    public string? ReplyId { get; init; }
    /// <summary>
    /// 平台事件 id，频道事件被动回复时带上
    /// </summary>
    public string? ReplyEventId { get; init; }
}
