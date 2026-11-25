using System.Text.Json.Serialization;

namespace PlugBotSharp.QQLib;

/// <summary>
/// 事件里的发送者
/// </summary>
public record QQAuthorObj
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("username")]
    public string? Username { get; set; }
    [JsonPropertyName("bot")]
    public bool Bot { get; set; }
    [JsonPropertyName("member_openid")]
    public string? MemberOpenId { get; set; }
    [JsonPropertyName("user_openid")]
    public string? UserOpenId { get; set; }
    [JsonPropertyName("member_role")]
    public string? MemberRole { get; set; }
}

/// <summary>
/// 事件里的附件
/// </summary>
public record QQAttachmentObj
{
    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    [JsonPropertyName("size")]
    public long Size { get; set; }
    [JsonPropertyName("width")]
    public int Width { get; set; }
    [JsonPropertyName("height")]
    public int Height { get; set; }
}

/// <summary>
/// 事件里的 @ 信息
/// </summary>
public record QQMentionObj
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("username")]
    public string? Username { get; set; }
    [JsonPropertyName("member_openid")]
    public string? MemberOpenId { get; set; }
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
    [JsonPropertyName("is_you")]
    public bool IsYou { get; set; }
}

/// <summary>
/// 群消息事件
/// </summary>
public record QQGroupMessageObj
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("content")]
    public string? Content { get; set; }
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
    [JsonPropertyName("group_openid")]
    public string? GroupOpenId { get; set; }
    [JsonPropertyName("group_id")]
    public string? GroupId { get; set; }
    [JsonPropertyName("author")]
    public QQAuthorObj? Author { get; set; }
    [JsonPropertyName("attachments")]
    public List<QQAttachmentObj>? Attachments { get; set; }
    [JsonPropertyName("mentions")]
    public List<QQMentionObj>? Mentions { get; set; }
}

/// <summary>
/// 单聊消息事件
/// </summary>
public record QQC2CMessageObj
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("content")]
    public string? Content { get; set; }
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
    [JsonPropertyName("author")]
    public QQAuthorObj? Author { get; set; }
    [JsonPropertyName("attachments")]
    public List<QQAttachmentObj>? Attachments { get; set; }
}
