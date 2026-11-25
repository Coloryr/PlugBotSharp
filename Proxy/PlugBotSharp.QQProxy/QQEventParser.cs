using System.Text.RegularExpressions;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.QQLib;

namespace PlugBotSharp.QQProxy;

/// <summary>
/// 把平台的回调事件转成通用的消息事件
/// </summary>
internal static partial class QQEventParser
{
    /// <summary>
    /// 群内消息，不要求 @ 机器人
    /// </summary>
    private const string GroupMessage = "GROUP_MESSAGE_CREATE";

    /// <summary>
    /// 解析事件，不是消息事件时返回 null
    /// </summary>
    /// <param name="proxyId">代理组件 id</param>
    /// <param name="uuid">实例 uuid</param>
    /// <param name="envelope">回调信封</param>
    public static ProxyMessage? Parse(string proxyId, Guid uuid, BaseMessageObj envelope)
    {
        try
        {
            return envelope.Type switch
            {
                EventNames.GroupAtMessageCreate or GroupMessage => ParseGroup(proxyId, uuid, envelope),
                EventNames.GroupMessageCreate => ParseC2C(proxyId, uuid, envelope),
                _ => null
            };
        }
        catch (Exception e)
        {
            // 解析失败要把报文留下来，不然只能看到一句"收到事件"
            ProxyInstance.Logger.Error(e, ProxyLang.GetLang(ProxyLangKey.EventParseError, envelope.Type, envelope.Data));

            return null;
        }
    }

    /// <summary>
    /// 群消息
    /// </summary>
    private static ProxyMessage? ParseGroup(string proxyId, Guid uuid, BaseMessageObj envelope)
    {
        var data = envelope.Data == null
            ? null
            : JsonUtils.ToObj(envelope.Data.ToJsonString(), QQJsonContext.Default.QQGroupMessageObj);

        if (data is null)
        {
            return null;
        }

        return new ProxyMessage
        {
            Id = proxyId,
            Uuid = uuid,
            SenderType = MessageSenderType.Group,
            Sender = data.Author?.MemberOpenId ?? data.Author?.Id ?? "",
            Target = data.GroupOpenId ?? data.GroupId ?? "",
            ReplyId = data.Id,
            Message = Build(data.Content, data.Attachments)
        };
    }

    /// <summary>
    /// 单聊消息
    /// </summary>
    private static ProxyMessage? ParseC2C(string proxyId, Guid uuid, BaseMessageObj envelope)
    {
        var data = envelope.Data == null
            ? null
            : JsonUtils.ToObj(envelope.Data.ToJsonString(), QQJsonContext.Default.QQC2CMessageObj);

        if (data is null)
        {
            return null;
        }

        return new ProxyMessage
        {
            Id = proxyId,
            Uuid = uuid,
            SenderType = MessageSenderType.User,
            Sender = data.Author?.UserOpenId ?? data.Author?.Id ?? "",
            ReplyId = data.Id,
            Message = Build(data.Content, data.Attachments)
        };
    }

    /// <summary>
    /// 文本和附件组成通用消息
    /// </summary>
    private static BaseMessage Build(string? content, List<QQAttachmentObj>? attachments)
    {
        var text = CleanContent(content);
        var media = new List<BaseMessage>();

        foreach (var item in attachments ?? [])
        {
            if (string.IsNullOrWhiteSpace(item.Url))
            {
                continue;
            }

            media.Add(IsImage(item.ContentType)
                ? new ImageUrlMessage(item.Url, GuessImageType(item.Url))
                : new FileUrlMessage(item.Url));
        }

        if (media.Count == 0)
        {
            return new TextMessage(text);
        }

        if (text.Length == 0)
        {
            return media.Count == 1 ? media[0] : new ListMessage([.. media]);
        }

        return new ListMessage([new TextMessage(text), .. media]);
    }

    /// <summary>
    /// 去掉 content 里的 @ 标记，和平台一样把开头的空格去掉
    /// </summary>
    private static string CleanContent(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return "";
        }

        var text = MentionRegex().Replace(content, " ");
        text = MentionTagRegex().Replace(text, " ");

        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    /// <summary>
    /// 附件是不是图片
    /// </summary>
    private static bool IsImage(string? contentType)
    {
        return contentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
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

    [GeneratedRegex(@"<@!?[^>]+>|<qqbot-at-user[^>]*/>", RegexOptions.IgnoreCase)]
    private static partial Regex MentionRegex();

    [GeneratedRegex(@"</?[a-z][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MentionTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}