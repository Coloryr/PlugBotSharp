using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.OneBotLib;

/// <summary>
/// 通用消息和 OneBot 消息段之间的转换，v11 和 v12 的段结构一样，只有 at / mention 的命名不同
/// </summary>
public static class OneBotMessage
{
    /// <summary>
    /// 直通消息的名字，Payload 是一段消息段的 JSON
    /// </summary>
    public const string SegmentName = OneBotSegment.Name;

    /// <summary>
    /// 消息段转通用消息
    /// </summary>
    /// <param name="message">message 字段，数组或者 CQ 字符串</param>
    /// <param name="replyId">引用的消息 id</param>
    /// <param name="version">协议版本</param>
    public static BaseMessage FromSegments(JsonElement message, out string? replyId, OneBotVersion version = OneBotVersion.V11)
    {
        replyId = null;

        if (message.ValueKind == JsonValueKind.String)
        {
            return new TextMessage(message.GetString() ?? "");
        }

        var text = new StringBuilder();
        var parts = new List<BaseMessage>();

        foreach (var segment in message.EnumerateArray())
        {
            if (!segment.TryGetProperty("type", out var typeNode)
                || !segment.TryGetProperty("data", out var data))
            {
                continue;
            }

            switch (typeNode.GetString())
            {
                case "text":
                    text.Append(data.TryGetProperty("text", out var t) ? t.GetString() : "");
                    break;
                case "at":
                    parts.Add(new MentionMessage(GetString(data, "qq") ?? "", GetString(data, "name"))
                    {
                        Everyone = GetString(data, "qq") == "all"
                    });
                    break;
                case "mention":
                    parts.Add(new MentionMessage(GetString(data, "user_id") ?? "", GetString(data, "name")));
                    break;
                case "mention_all":
                    parts.Add(new MentionMessage { Everyone = true });
                    break;
                case "file":
                    parts.Add(new FileUrlMessage(GetString(data, "file_id") ?? GetString(data, "url") ?? ""));
                    break;
                case "image":
                    parts.Add(ToImage(data));
                    break;
                case "face":
                    parts.Add(new FaceMessage
                    {
                        Id = GetString(data, "id") ?? "",
                        Name = GetString(data, "name")
                    });
                    break;
                case "mface":
                    parts.Add(new FaceMessage
                    {
                        Id = GetString(data, "emoji_id") ?? GetString(data, "emoji_package_id") ?? "",
                        Name = GetString(data, "key") ?? GetString(data, "summary"),
                        Market = true
                    });
                    break;
                case "record":
                    parts.Add(new RecordMessage
                    {
                        Url = GetString(data, "url"),
                        File = GetString(data, "file")
                    });
                    break;
                case "video":
                    parts.Add(new VideoMessage
                    {
                        Url = GetString(data, "url"),
                        File = GetString(data, "file"),
                        Cover = GetString(data, "cover")
                    });
                    break;
                case "json":
                    parts.Add(new CardMessage { Data = GetString(data, "data") ?? "" });
                    break;
                case "markdown":
                    parts.Add(new MarkdownMessage(GetString(data, "content") ?? GetString(data, "data") ?? ""));
                    break;
                case "xml":
                    parts.Add(new CardMessage { Data = GetString(data, "data") ?? "", Xml = true });
                    break;
                case "reply":
                    replyId = GetString(data, "id");
                    break;
            }
        }

        var content = text.ToString();

        if (parts.Count == 0)
        {
            return new TextMessage(content);
        }

        if (content.Length > 0)
        {
            parts.Insert(0, new TextMessage(content));
        }

        return parts.Count == 1 ? parts[0] : new ListMessage([.. parts]);
    }

    /// <summary>
    /// 通用消息转消息段
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="version">协议版本</param>
    public static JsonArray ToSegments(BaseMessage message, OneBotVersion version = OneBotVersion.V11)
    {
        var array = new JsonArray();

        Append(array, message, version);

        return array;
    }

    /// <summary>
    /// 把回复消息放到最前面
    /// </summary>
    /// <param name="array">消息段</param>
    /// <param name="replyId">引用的消息 id</param>
    public static JsonArray WithReply(JsonArray array, string? replyId)
    {
        if (string.IsNullOrEmpty(replyId) || HasReply(array))
        {
            return array;
        }

        var reply = new JsonObject
        {
            ["type"] = "reply",
            ["data"] = new JsonObject { ["id"] = replyId }
        };

        array.Insert(0, reply);

        return array;
    }

    /// <summary>
    /// 消息里是不是已经有回复段了
    /// </summary>
    private static bool HasReply(JsonArray array)
    {
        foreach (var node in array)
        {
            if (node?["type"]?.GetValue<string>() == "reply")
            {
                return true;
            }
        }

        return false;
    }

    private static void Append(JsonArray array, BaseMessage message, OneBotVersion version)
    {
        switch (message)
        {
            case ListMessage list:
                foreach (var item in list.Messages)
                {
                    Append(array, item, version);
                }

                break;
            case TextMessage text:
                array.Add(Segment("text", new JsonObject { ["text"] = text.Text }));
                break;
            case MentionMessage mention when version == OneBotVersion.V12:
                array.Add(mention.Everyone
                    ? Segment("mention_all", new JsonObject())
                    : Segment("mention", new JsonObject
                    {
                        ["user_id"] = mention.UserId,
                        ["name"] = mention.Name
                    }));
                break;
            case MentionMessage mention:
                array.Add(Segment("at", new JsonObject
                {
                    ["qq"] = mention.Everyone ? "all" : mention.UserId,
                    ["name"] = mention.Name
                }));
                break;
            case FaceMessage face:
                array.Add(Segment(face.Market ? "mface" : "face", new JsonObject
                {
                    ["id"] = face.Id,
                    ["key"] = face.Name
                }));
                break;
            case ImageUrlMessage image:
                array.Add(Segment("image", new JsonObject { ["file"] = image.Url }));
                break;
            case ImageFileMessage image:
                array.Add(Segment("image", new JsonObject { ["file"] = image.File }));
                break;
            case RecordMessage record:
                array.Add(Segment("record", new JsonObject { ["file"] = record.Url ?? record.File }));
                break;
            case VideoMessage video:
                array.Add(Segment("video", new JsonObject { ["file"] = video.Url ?? video.File }));
                break;
            case CardMessage card:
                array.Add(Segment(card.Xml ? "xml" : "json", new JsonObject { ["data"] = card.Data }));
                break;
            case MarkdownMessage markdown:
                array.Add(Segment("markdown", new JsonObject { ["content"] = markdown.Content }));
                break;
            case ReplyMessage reply:
                array.Add(Segment("reply", new JsonObject { ["id"] = reply.MessageId }));
                break;
            case RawMessage raw when raw.Name == SegmentName:
                array.Add(JsonNode.Parse(raw.Payload.ToJsonString()));
                break;
        }
    }

    /// <summary>
    /// 组一个消息段
    /// </summary>
    private static JsonObject Segment(string type, JsonObject data)
    {
        var clean = new JsonObject();

        foreach (var pair in data)
        {
            if (pair.Value is not null)
            {
                clean[pair.Key] = pair.Value.DeepClone();
            }
        }

        return new JsonObject
        {
            ["type"] = type,
            ["data"] = clean
        };
    }

    /// <summary>
    /// 图片段转图片消息
    /// </summary>
    private static BaseMessage ToImage(JsonElement data)
    {
        var url = GetString(data, "url");

        if (!string.IsNullOrEmpty(url))
        {
            return new ImageUrlMessage(url, GuessImageType(url));
        }

        return new ImageFileMessage(GetString(data, "file") ?? "", ImageType.Jpeg);
    }

    /// <summary>
    /// 从地址猜图片类型
    /// </summary>
    private static ImageType GuessImageType(string url)
    {
        var path = url.Split('?')[0].ToLowerInvariant();

        if (path.EndsWith(".png"))
        {
            return ImageType.Png;
        }

        return path.EndsWith(".webp") ? ImageType.Webp : ImageType.Jpeg;
    }

    /// <summary>
    /// 取字符串字段
    /// </summary>
    private static string? GetString(JsonElement data, string name)
    {
        if (!data.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }
}