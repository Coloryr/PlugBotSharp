namespace PlugBotSharp.Core.Messages;

/// <summary>
/// 回复消息，表示这条消息在引用哪一条：OneBot 的 reply 段、QQ 的 message_reference、Discord 的 message_reference
/// </summary>
public record ReplyMessage : BaseMessage
{
    public ReplyMessage()
    {
        Type = MessageType.Reply;
    }

    public ReplyMessage(string messageId)
    {
        Type = MessageType.Reply;
        MessageId = messageId;
    }

    /// <summary>
    /// 被引用的平台消息 id
    /// </summary>
    public string MessageId { get; init; } = "";
}
