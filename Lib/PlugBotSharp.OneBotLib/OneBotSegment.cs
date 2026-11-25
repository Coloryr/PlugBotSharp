using System.Text.Json.Nodes;
using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.OneBotLib;

/// <summary>
/// OneBot 11 的消息段，通用消息表达不了的结构用这些拼出来，再用 <see cref="Raw"/> 发出去
/// </summary>
public static class OneBotSegment
{
    /// <summary>
    /// 直通消息的名字，Payload 是一个消息段
    /// </summary>
    public const string Name = "onebot11.segment";

    /// <summary>
    /// 文本
    /// </summary>
    public static JsonObject Text(string text)
    {
        return Build("text", new JsonObject { ["text"] = text });
    }

    /// <summary>
    /// @ 某人，userId 为 all 时是 @ 全体；这个段名只适用于 v11，
    /// v12 请用 `OneBotMessage.ToSegments(new MentionMessage(...), OneBotVersion.V12)`
    /// </summary>
    public static JsonObject At(string userId, string? name = null)
    {
        return Build("at", new JsonObject { ["qq"] = userId, ["name"] = name });
    }

    /// <summary>
    /// 表情
    /// </summary>
    public static JsonObject Face(string id)
    {
        return Build("face", new JsonObject { ["id"] = id });
    }

    /// <summary>
    /// 图片，file 可以是文件名、路径、URL 或者 base64
    /// </summary>
    public static JsonObject Image(string file)
    {
        return Build("image", new JsonObject { ["file"] = file });
    }

    /// <summary>
    /// 语音
    /// </summary>
    public static JsonObject Record(string file)
    {
        return Build("record", new JsonObject { ["file"] = file });
    }

    /// <summary>
    /// 视频
    /// </summary>
    public static JsonObject Video(string file)
    {
        return Build("video", new JsonObject { ["file"] = file });
    }

    /// <summary>
    /// json 卡片
    /// </summary>
    public static JsonObject Json(string data)
    {
        return Build("json", new JsonObject { ["data"] = data });
    }

    /// <summary>
    /// xml 卡片
    /// </summary>
    public static JsonObject Xml(string data)
    {
        return Build("xml", new JsonObject { ["data"] = data });
    }

    /// <summary>
    /// markdown
    /// </summary>
    public static JsonObject Markdown(string content)
    {
        return Build("markdown", new JsonObject { ["content"] = content });
    }

    /// <summary>
    /// 引用某条消息
    /// </summary>
    public static JsonObject Reply(string id)
    {
        return Build("reply", new JsonObject { ["id"] = id });
    }

    /// <summary>
    /// 音乐分享，type 为 qq / 163 / xm 时只需要 id
    /// </summary>
    public static JsonObject Music(string type, string? id = null, string? url = null,
        string? audio = null, string? title = null, string? image = null)
    {
        return Build("music", new JsonObject
        {
            ["type"] = type,
            ["id"] = id,
            ["url"] = url,
            ["audio"] = audio,
            ["title"] = title,
            ["image"] = image
        });
    }

    /// <summary>
    /// 位置
    /// </summary>
    public static JsonObject Location(double lat, double lon, string? title = null, string? content = null)
    {
        return Build("location", new JsonObject
        {
            ["lat"] = lat,
            ["lon"] = lon,
            ["title"] = title,
            ["content"] = content
        });
    }

    /// <summary>
    /// 名片，group 为 true 时是群名片
    /// </summary>
    public static JsonObject Contact(bool group, string id)
    {
        return Build("contact", new JsonObject
        {
            ["type"] = group ? "group" : "qq",
            ["id"] = id
        });
    }

    /// <summary>
    /// 戳一戳，type 和 id 按 OneBot 实现的定义填
    /// </summary>
    public static JsonObject Poke(string type, string id)
    {
        return Build("poke", new JsonObject { ["type"] = type, ["id"] = id });
    }

    /// <summary>
    /// 窗口抖动
    /// </summary>
    public static JsonObject Shake()
    {
        return Build("shake", new JsonObject());
    }

    /// <summary>
    /// 骰子，result 不填由实现随机
    /// </summary>
    public static JsonObject Dice(int? result = null)
    {
        return Build("dice", new JsonObject { ["result"] = result });
    }

    /// <summary>
    /// 猜拳，result 不填由实现随机
    /// </summary>
    public static JsonObject Rps(int? result = null)
    {
        return Build("rps", new JsonObject { ["result"] = result });
    }

    /// <summary>
    /// 合并转发，id 来自收到的 forward 段
    /// </summary>
    public static JsonObject Forward(string id)
    {
        return Build("forward", new JsonObject { ["id"] = id });
    }

    /// <summary>
    /// 分享链接
    /// </summary>
    public static JsonObject Share(string url, string title, string? content = null, string? image = null)
    {
        return Build("share", new JsonObject
        {
            ["url"] = url,
            ["title"] = title,
            ["content"] = content,
            ["image"] = image
        });
    }

    /// <summary>
    /// 把消息段包成直通消息
    /// </summary>
    /// <param name="segment">消息段</param>
    public static RawMessage Raw(JsonObject segment)
    {
        return new RawMessage(Name, segment);
    }

    /// <summary>
    /// 组一个消息段，值为 null 的字段丢掉
    /// </summary>
    public static JsonObject Build(string type, JsonObject data)
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
}