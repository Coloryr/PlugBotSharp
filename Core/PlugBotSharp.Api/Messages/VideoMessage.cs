namespace PlugBotSharp.Api.Messages;

/// <summary>
/// 视频消息
/// </summary>
public record VideoMessage : BaseMessage
{
    public VideoMessage()
    {
        Type = MessageType.Video;
    }

    /// <summary>
    /// 视频地址
    /// </summary>
    public string? Url { get; init; }
    /// <summary>
    /// 视频文件名 / 文件路径
    /// </summary>
    public string? File { get; init; }
    /// <summary>
    /// 封面地址
    /// </summary>
    public string? Cover { get; init; }
}
