namespace PlugBotSharp.Core.Messages;

/// <summary>
/// 语音消息
/// </summary>
public record RecordMessage : BaseMessage
{
    public RecordMessage()
    {
        Type = MessageType.Record;
    }

    /// <summary>
    /// 语音地址
    /// </summary>
    public string? Url { get; init; }
    /// <summary>
    /// 语音文件名 / 文件路径
    /// </summary>
    public string? File { get; init; }
    /// <summary>
    /// 时长（秒）
    /// </summary>
    public int Seconds { get; init; }
}
