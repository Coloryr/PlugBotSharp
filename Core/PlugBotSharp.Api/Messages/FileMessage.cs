namespace PlugBotSharp.Api.Messages;

/// <summary>
/// 文件消息
/// </summary>
public record FileMessage : BaseMessage
{
    public FileMessage(string file)
    {
        Type = MessageType.File;
        File = file;
    }

    /// <summary>
    /// 文件路径
    /// </summary>
    public string File { get; init; }
}

/// <summary>
/// 文件链接消息
/// </summary>
public record FileUrlMessage : BaseMessage
{
    public FileUrlMessage(string url)
    {
        Type = MessageType.FileUrl;
        Url = url;
    }

    /// <summary>
    /// 地址
    /// </summary>
    public string Url { get; init; }
}
