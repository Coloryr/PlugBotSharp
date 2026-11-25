namespace PlugBotSharp.Core.Messages;

/// <summary>
/// 列表消息，把多条消息合成一条发送
/// </summary>
public record ListMessage : BaseMessage
{
    public ListMessage(params BaseMessage[] list)
    {
        Type = MessageType.List;
        Messages = [.. list];
    }

    /// <summary>
    /// 消息列表
    /// </summary>
    public IReadOnlyList<BaseMessage> Messages { get; init; }
}
