using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace PlugBotSharp.Core.Messages;

/// <summary>
/// 消息类型
/// </summary>
public enum MessageType
{
    Text, Image, ImageFile, ImageUrl, File, FileUrl, List
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

public abstract record BaseMessage
{
    public MessageType Type { get; init; }
}

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

/// <summary>
/// 图片消息
/// </summary>
public record ImageMessage : BaseMessage
{
    public ImageMessage(Stream stream, ImageType imageType)
    {
        Type = MessageType.Image;
        ImageType = imageType;
        Image = new MemoryStream();
        stream.CopyTo(Image);
        Image.Seek(0, SeekOrigin.Begin);
    }

    /// <summary>
    /// 图片数据
    /// </summary>
    public MemoryStream Image { get; init; }
    /// <summary>
    /// 图片类型
    /// </summary>
    public ImageType ImageType { get; init; }
}

/// <summary>
/// 图片文件消息
/// </summary>
public record ImageFileMessage : BaseMessage
{
    public ImageFileMessage(string file, ImageType imageType)
    {
        Type = MessageType.ImageFile;
        ImageType = imageType;
        File = file;
    }

    /// <summary>
    /// 图片文件
    /// </summary>
    public string File { get; init; }
    /// <summary>
    /// 图片类型
    /// </summary>
    public ImageType ImageType { get; init; }
}

/// <summary>
/// 图片地址消息
/// </summary>
public record ImageUrlMessage : BaseMessage
{
    public ImageUrlMessage(string url, ImageType imageType)
    {
        Type = MessageType.ImageFile;
        ImageType = imageType;
        Url = url;
    }

    /// <summary>
    /// 地址
    /// </summary>
    public string Url { get; init; }
    /// <summary>
    /// 图片类型
    /// </summary>
    public ImageType ImageType { get; init; }
}

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

/// <summary>
/// 列表消息
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

/// <summary>
/// 端口消息
/// </summary>
public record ProxyMessage
{
    /// <summary>
    /// 端口
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// 实例
    /// </summary>
    public required Guid Uuid { get; init; }
    /// <summary>
    /// 发送者类型
    /// </summary>
    public required MessageSenderType SenderType { get; init; }
    /// <summary>
    /// 发送者
    /// </summary>
    public required string Sender { get; set; }
    /// <summary>
    /// 消息
    /// </summary>
    public required BaseMessage Message { get; init; }
}