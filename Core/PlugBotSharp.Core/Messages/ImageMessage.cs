namespace PlugBotSharp.Core.Messages;

/// <summary>
/// 图片消息，直接带图片数据
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
