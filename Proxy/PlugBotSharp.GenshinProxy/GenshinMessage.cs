using System.Text.Json;
using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.GenshinProxy;

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
