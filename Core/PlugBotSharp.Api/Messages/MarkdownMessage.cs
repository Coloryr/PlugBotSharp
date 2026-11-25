namespace PlugBotSharp.Core.Messages;

/// <summary>
/// markdown 文本，QQ 的 msg_type 2、OneBot v12 的 markdown 段、KOOK 的 KMarkdown 都对应这个
/// </summary>
public record MarkdownMessage : BaseMessage
{
    public MarkdownMessage()
    {
        Type = MessageType.Markdown;
    }

    public MarkdownMessage(string content)
    {
        Type = MessageType.Markdown;
        Content = content;
    }

    /// <summary>
    /// markdown 内容
    /// </summary>
    public string Content { get; init; } = "";
}
