using System.Text.Json;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.OneBotLib;

namespace PlugBotSharp.OneBotProxy;

/// <summary>
/// OneBot 11 事件转成通用消息事件
/// </summary>
internal static class OneBotEvent
{
    /// <summary>
    /// 解析事件，不是消息事件时返回 null
    /// </summary>
    /// <param name="proxyId">代理组件 id</param>
    /// <param name="uuid">实例 uuid</param>
    /// <param name="evt">上报的事件</param>
    public static ProxyMessage? Parse(string proxyId, Guid uuid, JsonElement evt)
    {
        if (!evt.TryGetProperty("post_type", out var postType)
            || postType.GetString() != "message"
            || !evt.TryGetProperty("message_type", out var messageType)
            || !evt.TryGetProperty("message", out var message))
        {
            return null;
        }

        var isGroup = messageType.GetString() == "group";
        var body = OneBotMessage.FromSegments(message, out _);

        return new ProxyMessage
        {
            Id = proxyId,
            Uuid = uuid,
            SenderType = isGroup ? MessageSenderType.Group : MessageSenderType.User,
            Sender = GetId(evt, "user_id"),
            Target = isGroup ? GetId(evt, "group_id") : "",
            ReplyId = GetId(evt, "message_id"),
            Message = body
        };
    }

    /// <summary>
    /// 取数字 id 转成字符串
    /// </summary>
    private static string GetId(JsonElement evt, string name)
    {
        if (!evt.TryGetProperty(name, out var value))
        {
            return "";
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.String => value.GetString() ?? "",
            _ => ""
        };
    }
}