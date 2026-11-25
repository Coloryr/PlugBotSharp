using System.Text.Json;
using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.QQLib;

/// <summary>
/// 通用消息和 QQ 消息体之间的转换
/// </summary>
public static class QQMessage
{
    /// <summary>
    /// 直通消息的名字，Payload 是 SendGroupMessageObj 的 JSON
    /// </summary>
    public const string SendName = "qq.send";

    /// <summary>
    /// 转成平台的消息体，转不了返回 null
    /// </summary>
    /// <param name="message">通用消息</param>
    /// <param name="replyId">被动回复的平台消息 id</param>
    /// <param name="replyEventId">被动回复的平台事件 id</param>
    /// <param name="sequence">回复序号</param>
    public static SendGroupMessageObj? From(BaseMessage message, string? replyId = null, string? replyEventId = null, int sequence = 0)
    {
        // 一条 QQ 消息只能有一个 msg_type，List 里取第一个能发的
        var obj = PickContent(message) switch
        {
            TextMessage text => new SendGroupMessageObj
            {
                MsgType = 0,
                Content = text.Text
            },
            MarkdownMessage markdown => new SendGroupMessageObj
            {
                MsgType = 2,
                Markdown = new MessageMarkdownObj { Content = markdown.Content }
            },
            RawMessage raw when raw.Name == SendName => raw.Payload.Deserialize(QQJsonContext.Default.SendGroupMessageObj),
            _ => null
        };

        if (obj is null)
        {
            return null;
        }

        // 内容里带 ReplyMessage 时转成引用回复
        if (FindReplyId(message) is { } quoted)
        {
            obj.MessageReference = new MessageReferenceObj { MessageId = quoted };
        }

        obj.MsgId = replyId;
        obj.EventId = replyEventId;
        obj.MsgSeq = sequence;

        return obj;
    }

    /// <summary>
    /// 取第一条真正能发出去的消息，跳过引用类型
    /// </summary>
    private static BaseMessage? PickContent(BaseMessage message)
    {
        if (message is not ListMessage list)
        {
            return message is ReplyMessage ? null : message;
        }

        foreach (var item in list.Messages)
        {
            if (PickContent(item) is { } picked)
            {
                return picked;
            }
        }

        return null;
    }

    /// <summary>
    /// 找内容里的引用消息 id
    /// </summary>
    private static string? FindReplyId(BaseMessage message)
    {
        return message switch
        {
            ReplyMessage reply => string.IsNullOrEmpty(reply.MessageId) ? null : reply.MessageId,
            ListMessage list => list.Messages.Select(FindReplyId).FirstOrDefault(x => x is not null),
            _ => null
        };
    }
}
