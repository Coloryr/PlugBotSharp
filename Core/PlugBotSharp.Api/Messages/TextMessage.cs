namespace PlugBotSharp.Api.Messages;

/// <summary>
/// 文本消息
/// </summary>
public record TextMessage : BaseMessage
{
    public TextMessage(string text)
    {
        Type = MessageType.Text;
        Text = text;
    }

    /// <summary>
    /// 文本内容
    /// </summary>
    public string Text { get; init; }
}
