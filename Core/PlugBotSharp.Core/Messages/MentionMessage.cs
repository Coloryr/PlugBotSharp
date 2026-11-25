namespace PlugBotSharp.Core.Messages;

/// <summary>
/// @ 消息，Everyone 为 true 时是 @ 全体
/// </summary>
public record MentionMessage : BaseMessage
{
    public MentionMessage()
    {
        Type = MessageType.Mention;
    }

    public MentionMessage(string userId, string? name = null)
    {
        Type = MessageType.Mention;
        UserId = userId;
        Name = name;
    }

    /// <summary>
    /// 被 @ 的用户 id，@ 全体时为空
    /// </summary>
    public string UserId { get; init; } = "";
    /// <summary>
    /// 被 @ 的用户名
    /// </summary>
    public string? Name { get; init; }
    /// <summary>
    /// 是否 @ 全体
    /// </summary>
    public bool Everyone { get; init; }
}
