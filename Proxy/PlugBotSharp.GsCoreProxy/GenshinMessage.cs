using System.Text.Json;
using System.Text.Json.Nodes;
using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.GsCoreProxy;

/// <summary>
/// 通用消息转 gsuid_core 的消息段
/// </summary>
public static class GenshinMessage
{
    /// <summary>
    /// 直通消息的名字，Payload 是一段消息段
    /// </summary>
    public const string SegmentName = "genshin.segment";

    /// <summary>
    /// link:// 前缀，表示后面是网址
    /// </summary>
    private const string LinkPrefix = "link://";

    /// <summary>
    /// base64:// 前缀，表示后面是文件内容
    /// </summary>
    private const string Base64Prefix = "base64://";

    /// <summary>
    /// 通用消息转消息段
    /// </summary>
    /// <param name="message">消息</param>
    public static List<GsMessage> ToGs(BaseMessage message)
    {
        var list = new List<GsMessage>();

        Append(list, message);

        return list;
    }

    /// <summary>
    /// core 下发的消息段转通用消息
    /// </summary>
    /// <param name="segments">消息段</param>
    /// <param name="replyId">段里带的 reply_id，也就是被引用的消息</param>
    public static List<BaseMessage> FromGs(IEnumerable<GsMessage> segments, out string? replyId)
    {
        var list = new List<BaseMessage>();
        var reply = (string?)null;

        foreach (var segment in segments)
        {
            Append(segment);
        }

        replyId = reply;

        return list;

        void Append(GsMessage segment)
        {
            switch (segment.Type)
            {
                case "text":
                    if (Data(segment) is { Length: > 0 } text)
                    {
                        list.Add(new TextMessage(text));
                    }

                    break;
                case "at":
                    var at = Data(segment);

                    list.Add(string.Equals(at, "all", StringComparison.OrdinalIgnoreCase)
                        ? new MentionMessage { Everyone = true }
                        : new MentionMessage(at ?? ""));
                    break;
                case "image":
                    if (ToImage(Data(segment)) is { } image)
                    {
                        list.Add(image);
                    }

                    break;
                case "record":
                    list.Add(ToRecord(Data(segment)));
                    break;
                case "video":
                    list.Add(ToVideo(Data(segment)));
                    break;
                case "file":
                    list.Add(ToFile(Data(segment)));
                    break;
                case "markdown":
                    if (Data(segment) is { Length: > 0 } markdown)
                    {
                        list.Add(new MarkdownMessage(markdown));
                    }

                    break;
                case "reply_id":
                    reply = Data(segment);

                    if (!string.IsNullOrEmpty(reply))
                    {
                        list.Add(new ReplyMessage(reply));
                    }

                    break;
                case "json":
                    if (Data(segment) is { Length: > 0 } json)
                    {
                        list.Add(new CardMessage { Data = json });
                    }

                    break;
                case "xml":
                    if (Data(segment) is { Length: > 0 } xml)
                    {
                        list.Add(new CardMessage { Data = xml, Xml = true });
                    }

                    break;
                case "node":
                    // core 的合并转发节点，QQ 发不了合并转发就拆开成普通消息
                    if (segment.Data is JsonArray node)
                    {
                        foreach (var item in node.Deserialize(GenshinJsonContext.Default.ListGsMessage) ?? [])
                        {
                            Append(item);
                        }
                    }

                    break;
                case "group":
                    // 目标群标记，发送时用不到
                    break;
                default:
                    // 不认识的段原样交给下游代理解释
                    if (!string.IsNullOrEmpty(segment.Type))
                    {
                        var raw = JsonSerializer.SerializeToNode(segment, GenshinJsonContext.Default.GsMessage)!;

                        GenshinLang.Log.Debug($"未知消息段: {raw.ToJsonString()}");

                        list.Add(new RawMessage(SegmentName, raw));
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// 取段的字符串内容，不是字符串就返回原始 json
    /// </summary>
    private static string? Data(GsMessage segment)
    {
        if (segment.Data is null)
        {
            return null;
        }

        return segment.Data.GetValueKind() == JsonValueKind.String
            ? segment.Data.GetValue<string>()
            : segment.Data.ToJsonString();
    }

    /// <summary>
    /// 图片段转图片消息，base64 直接读进内存
    /// </summary>
    private static BaseMessage? ToImage(string? data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return null;
        }

        if (data.StartsWith(LinkPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new ImageUrlMessage(data[LinkPrefix.Length..], ImageType.Png);
        }

        if (data.StartsWith(Base64Prefix, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var stream = new MemoryStream(Convert.FromBase64String(data[Base64Prefix.Length..]));

                return new ImageMessage(stream, ImageType.Png);
            }
            catch
            {
                return null;
            }
        }

        return new ImageUrlMessage(data, ImageType.Png);
    }

    /// <summary>
    /// 语音段转语音消息
    /// </summary>
    private static RecordMessage ToRecord(string? data)
    {
        var (url, file) = SplitMedia(data, ".mp3");

        return new RecordMessage { Url = url, File = file };
    }

    /// <summary>
    /// 视频段转视频消息
    /// </summary>
    private static VideoMessage ToVideo(string? data)
    {
        var (url, file) = SplitMedia(data, ".mp4");

        return new VideoMessage { Url = url, File = file };
    }

    /// <summary>
    /// 文件段转文件消息
    /// </summary>
    private static BaseMessage ToFile(string? data)
    {
        var (url, file) = SplitMedia(data, ".bin");

        return url is not null ? new FileUrlMessage(url) : new FileMessage(file ?? "");
    }

    /// <summary>
    /// link:// 或 http 的是地址，base64:// 落成临时文件，其余当本地路径
    /// </summary>
    private static (string? Url, string? File) SplitMedia(string? data, string ext)
    {
        if (string.IsNullOrEmpty(data))
        {
            return (null, "");
        }

        if (data.StartsWith(LinkPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (data[LinkPrefix.Length..], null);
        }

        if (data.StartsWith(Base64Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return (null, WriteTemp(data[Base64Prefix.Length..], ext));
        }

        return data.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? (data, null) : (null, data);
    }

    /// <summary>
    /// base64 内容落临时文件，写不进去就把原串当文件名
    /// </summary>
    private static string WriteTemp(string base64, string ext)
    {
        try
        {
            var folder = Path.Combine(Path.GetTempPath(), "plugbotsharp", "gscore");

            Directory.CreateDirectory(folder);

            var file = Path.Combine(folder, Guid.NewGuid().ToString("N") + ext);

            System.IO.File.WriteAllBytes(file, Convert.FromBase64String(base64));

            return file;
        }
        catch
        {
            return Base64Prefix + base64;
        }
    }

    /// <summary>
    /// 把一条消息转成消息段
    /// </summary>
    private static void Append(List<GsMessage> list, BaseMessage message)
    {
        switch (message)
        {
            case ListMessage multi:
                foreach (var item in multi.Messages)
                {
                    Append(list, item);
                }

                break;
            case TextMessage text:
                list.Add(GsMessage.Of("text", text.Text));
                break;
            case MentionMessage mention:
                list.Add(GsMessage.Of("at", mention.Everyone ? "all" : mention.UserId));
                break;
            case ImageUrlMessage image:
                list.Add(GsMessage.Of("image", "link://" + image.Url));
                break;
            case ImageFileMessage image:
                list.Add(GsMessage.Of("image", ToLink(image.File)));
                break;
            case ImageMessage image when image.Image is not null:
                list.Add(GsMessage.Of("image", "base64://" + Convert.ToBase64String(image.Image.ToArray())));
                break;
            case FileUrlMessage file:
                list.Add(GsMessage.Of("file", "link://" + file.Url));
                break;
            case FileMessage file:
                list.Add(GsMessage.Of("file", ToLink(file.File)));
                break;
            case RecordMessage record:
                list.Add(GsMessage.Of("record", ToLink(record.Url ?? record.File)));
                break;
            case VideoMessage video:
                list.Add(GsMessage.Of("video", ToLink(video.Url ?? video.File)));
                break;
            case MarkdownMessage markdown:
                list.Add(GsMessage.Of("markdown", markdown.Content));
                break;
            case ReplyMessage reply:
                list.Add(GsMessage.Of("reply_id", reply.MessageId));
                break;
            case RawMessage raw when raw.Name == SegmentName:
                list.Add(JsonSerializer.Deserialize(raw.Payload, GenshinJsonContext.Default.GsMessage)!);
                break;
        }
    }

    /// <summary>
    /// 本地文件转 base64，网址原样加前缀
    /// </summary>
    private static string ToLink(string? file)
    {
        if (string.IsNullOrEmpty(file))
        {
            return "";
        }

        if (file.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return "link://" + file;
        }

        try
        {
            return System.IO.File.Exists(file)
                ? "base64://" + Convert.ToBase64String(System.IO.File.ReadAllBytes(file))
                : "link://" + file;
        }
        catch
        {
            return "link://" + file;
        }
    }
}
