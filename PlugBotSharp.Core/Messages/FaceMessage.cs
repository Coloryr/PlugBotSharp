namespace PlugBotSharp.Core.Messages;

/// <summary>
/// 表情消息，Market 为 true 时是表情包（mface）
/// </summary>
public record FaceMessage : BaseMessage
{
    public FaceMessage()
    {
        Type = MessageType.Face;
    }

    /// <summary>
    /// 表情 id
    /// </summary>
    public string Id { get; init; } = "";
    /// <summary>
    /// 表情名 / 表情包 key
    /// </summary>
    public string? Name { get; init; }
    /// <summary>
    /// 是不是表情包，表情包一般要带上 Name 一起发
    /// </summary>
    public bool Market { get; init; }
}
