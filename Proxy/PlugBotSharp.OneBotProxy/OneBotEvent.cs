using System.Text.Json;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.OneBotLib;

namespace PlugBotSharp.OneBotProxy;

/// <summary>
/// OneBot 事件转成通用消息事件
/// </summary>
internal static class OneBotEvent
{
    /// <summary>
    /// 解析事件，不是消息事件时返回 null
    /// </summary>
    /// <param name="proxyId">代理组件 id</param>
    /// <param name="uuid">实例 uuid</param>
    /// <param name="evt">上报的事件</param>
    /// <param name="version">协议版本</param>
    public static ProxyMessage? Parse(string proxyId, Guid uuid, JsonElement evt, OneBotVersion version = OneBotVersion.V11)
    {
        return version == OneBotVersion.V12
            ? ParseV12(proxyId, uuid, evt)
            : ParseV11(proxyId, uuid, evt);
    }

    /// <summary>
    /// v11：post_type=message，message_type 区分群聊 / 私聊
    /// </summary>
    private static ProxyMessage? ParseV11(string proxyId, Guid uuid, JsonElement evt)
    {
        if (!evt.TryGetProperty("post_type", out var postType)
            || postType.GetString() != "message"
            || !evt.TryGetProperty("message_type", out var messageType)
            || !evt.TryGetProperty("message", out var message))
        {
            return null;
        }

        var isGroup = messageType.GetString() == "group";

        return new ProxyMessage
        {
            Id = proxyId,
            Uuid = uuid,
            SenderType = isGroup ? MessageSenderType.Group : MessageSenderType.User,
            Sender = GetId(evt, "user_id"),
            Target = isGroup ? GetId(evt, "group_id") : "",
            ReplyId = GetId(evt, "message_id"),
            Message = OneBotMessage.FromSegments(message, out _, OneBotVersion.V11)
        };
    }

    /// <summary>
    /// v12：type=message，detail_type 区分群聊 / 私聊
    /// </summary>
    private static ProxyMessage? ParseV12(string proxyId, Guid uuid, JsonElement evt)
    {
        if (!evt.TryGetProperty("type", out var type)
            || type.GetString() != "message"
            || !evt.TryGetProperty("detail_type", out var detailType)
            || !evt.TryGetProperty("message", out var message))
        {
            return null;
        }

        var isGroup = detailType.GetString() == "group";

        return new ProxyMessage
        {
            Id = proxyId,
            Uuid = uuid,
            SenderType = isGroup ? MessageSenderType.Group : MessageSenderType.User,
            Sender = GetId(evt, "user_id"),
            Target = isGroup ? GetId(evt, "group_id") : "",
            ReplyId = GetId(evt, "message_id"),
            Message = OneBotMessage.FromSegments(message, out _, OneBotVersion.V12)
        };
    }

    /// <summary>
    /// 取 id，v11 是数字，v12 是字符串
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
