using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.Core.Http;

/// <summary>
/// 面板调试终端：注入的消息用这个 uuid 当"实例"，发往它的回复会被截下来给面板看，不会真的发到平台
/// </summary>
internal static class DebugSink
{
    /// <summary>
    /// 调试终端占用的实例 uuid，和真实实例不会撞
    /// </summary>
    public static readonly Guid Uuid = Guid.Parse("00000000-0000-0000-0000-0000000deb00");

    /// <summary>
    /// 图片预览上限，超过就不给预览
    /// </summary>
    private const int MaxPreviewBytes = 8 * 1024 * 1024;

    private static readonly List<CapturedReply> s_items = [];
    private static readonly Dictionary<long, byte[]> s_images = [];
    private static readonly Lock s_lock = new();

    private static long s_seq;

    /// <summary>
    /// 开始新一轮调试，清掉上一轮的记录
    /// </summary>
    /// <returns>当前的序号</returns>
    public static long Clear()
    {
        lock (s_lock)
        {
            s_items.Clear();
            s_images.Clear();

            return s_seq;
        }
    }

    /// <summary>
    /// 记一条发往调试终端的回复
    /// </summary>
    /// <param name="message">要发出的消息</param>
    public static void Capture(BaseMessage message)
    {
        var seq = ++s_seq;
        string? mime = null;

        if (message is ImageMessage image && image.Image.Length <= MaxPreviewBytes)
        {
            mime = Mime(image.ImageType);

            lock (s_lock)
            {
                s_images[seq] = image.Image.ToArray();
            }
        }

        var reply = new CapturedReply(seq, message.Type.ToString(), Describe(message), mime, DateTime.Now);

        lock (s_lock)
        {
            s_items.Add(reply);
        }
    }

    /// <summary>
    /// 取序号大于 since 的回复
    /// </summary>
    /// <param name="since">上次拿到的序号</param>
    public static List<CapturedReply> Since(long since)
    {
        lock (s_lock)
        {
            return s_items.Where(x => x.Seq > since).ToList();
        }
    }

    /// <summary>
    /// 取一条回复的图片数据
    /// </summary>
    /// <param name="seq">回复序号</param>
    public static byte[]? Image(long seq)
    {
        lock (s_lock)
        {
            return s_images.TryGetValue(seq, out var data) ? data : null;
        }
    }

    /// <summary>
    /// 消息转成一段文本
    /// </summary>
    /// <param name="message">消息</param>
    public static string Describe(BaseMessage message)
    {
        return message switch
        {
            TextMessage text => text.Text,
            MarkdownMessage markdown => markdown.Content,
            ImageUrlMessage image => image.Url,
            ImageFileMessage image => image.File,
            ImageMessage image => "[图片 " + (image.Image.Length / 1024) + " KB]",
            FileMessage file => file.File,
            FileUrlMessage file => file.Url,
            ReplyMessage reply => "引用 " + reply.MessageId,
            ListMessage list => string.Join(" | ", list.Messages.Select(Describe)),
            RawMessage raw => "[" + raw.Name + "]",
            _ => "[" + message.Type + "]"
        };
    }

    /// <summary>
    /// 图片类型转 mime
    /// </summary>
    /// <param name="type">图片类型</param>
    public static string Mime(ImageType type)
    {
        return type switch
        {
            ImageType.Png => "image/png",
            ImageType.Webp => "image/webp",
            _ => "image/jpeg"
        };
    }
}

/// <summary>
/// 面板调试终端截下来的一条回复
/// </summary>
/// <param name="Seq">序号</param>
/// <param name="Type">消息类型</param>
/// <param name="Text">内容摘要</param>
/// <param name="PreviewMime">图片的 mime，非空表示可以取预览</param>
/// <param name="Time">时间</param>
internal sealed record CapturedReply(long Seq, string Type, string Text, string? PreviewMime, DateTime Time);
