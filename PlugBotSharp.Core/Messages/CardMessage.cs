namespace PlugBotSharp.Core.Messages;

/// <summary>
/// json / xml 卡片
/// </summary>
public record CardMessage : BaseMessage
{
    public CardMessage()
    {
        Type = MessageType.Card;
    }

    /// <summary>
    /// 卡片内容
    /// </summary>
    public string Data { get; init; } = "";
    /// <summary>
    /// 是不是 xml 卡片
    /// </summary>
    public bool Xml { get; init; }
}
