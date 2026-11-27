namespace PlugBotSharp.Api.Messages;

/// <summary>
/// 消息类型
/// </summary>
public enum MessageType
{
    Text, Image, ImageFile, ImageUrl, File, FileUrl, List,
    /// <summary>
    /// @ 某人或 @ 全体
    /// </summary>
    Mention,
    /// <summary>
    /// 表情
    /// </summary>
    Face,
    /// <summary>
    /// 语音
    /// </summary>
    Record,
    /// <summary>
    /// 视频
    /// </summary>
    Video,
    /// <summary>
    /// json / xml 卡片
    /// </summary>
    Card,
    /// <summary>
    /// markdown 文本
    /// </summary>
    Markdown,
    /// <summary>
    /// 回复（引用某条消息）
    /// </summary>
    Reply,
    /// <summary>
    /// 直通消息，内容由代理自己解释
    /// </summary>
    Raw
}

/// <summary>
/// 消息发送者类型
/// </summary>
public enum MessageSenderType
{
    /// <summary>
    /// 用户私聊
    /// </summary>
    User,
    /// <summary>
    /// 群对话
    /// </summary>
    Group,
    /// <summary>
    /// 频道对话
    /// </summary>
    Guild,
    /// <summary>
    /// 频道私聊
    /// </summary>
    GuildUser,
    /// <summary>
    /// 其他类型
    /// </summary>
    Any
}

/// <summary>
/// 图片类型
/// </summary>
public enum ImageType
{
    Jpeg, Png, Webp
}