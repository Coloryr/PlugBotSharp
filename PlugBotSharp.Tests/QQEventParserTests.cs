using System.Text.Json;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.QQProxy;

namespace PlugBotSharp.Tests;

/// <summary>
/// 平台事件解析成通用消息事件
/// </summary>
public class QQEventParserTests
{
    private const string GroupEvent = """
        {
          "op": 0,
          "t": "GROUP_MESSAGE_CREATE",
          "id": "GROUP_MESSAGE_CREATE:test",
          "d": {
            "id": "msg-1",
            "content": " <@REPLIED_USER> zssm",
            "timestamp": "2026-08-05T12:51:27+08:00",
            "group_openid": "GROUP",
            "group_id": "GROUP",
            "author": { "id": "SENDER", "username": "sender", "bot": false, "member_openid": "SENDER" },
            "mentions": [ { "id": "REPLIED_USER", "username": "target", "member_openid": "REPLIED_USER", "scope": "single", "is_you": false } ]
          }
        }
        """;

    private const string C2CEvent = """
        {
          "op": 0,
          "t": "C2C_MESSAGE_CREATE",
          "d": {
            "id": "msg-2",
            "content": "hello",
            "author": { "id": "USER", "user_openid": "USER_OPENID" }
          }
        }
        """;

    [Fact]
    public void Group_message_becomes_a_group_proxy_message()
    {
        var message = Parse(GroupEvent)!;

        Assert.Equal(MessageSenderType.Group, message.SenderType);
        Assert.Equal("SENDER", message.Sender);
        Assert.Equal("GROUP", message.Target);
        Assert.Equal("msg-1", message.ReplyId);
        Assert.Equal("zssm", Assert.IsType<TextMessage>(message.Message).Text);
    }

    [Fact]
    public void C2C_message_becomes_a_user_proxy_message()
    {
        var message = Parse(C2CEvent)!;

        Assert.Equal(MessageSenderType.User, message.SenderType);
        Assert.Equal("USER_OPENID", message.Sender);
        Assert.Equal("", message.Target);
        Assert.Equal("msg-2", message.ReplyId);
        Assert.Equal("hello", Assert.IsType<TextMessage>(message.Message).Text);
    }

    [Fact]
    public void Message_with_only_an_attachment_becomes_that_attachment()
    {
        var message = Parse("""
            {
              "op": 0,
              "t": "GROUP_AT_MESSAGE_CREATE",
              "d": {
                "id": "msg-3",
                "content": "",
                "group_openid": "GROUP",
                "author": { "id": "SENDER", "member_openid": "SENDER" },
                "attachments": [ { "content_type": "image/png", "url": "https://example.com/a.png" } ]
              }
            }
            """)!;

        var image = Assert.IsType<ImageUrlMessage>(message.Message);
        Assert.Equal(ImageType.Png, image.ImageType);
        Assert.Equal("https://example.com/a.png", image.Url);
    }

    [Fact]
    public void Other_events_are_ignored()
    {
        Assert.Null(Parse("""{"op":0,"t":"GROUP_ADD_ROBOT","d":{}}"""));
    }

    /// <summary>
    /// 解析一条事件
    /// </summary>
    private static ProxyMessage? Parse(string json)
    {
        var envelope = JsonSerializer.Deserialize(json, JsonContext.Default.BaseMessageObj)!;

        return QQEventParser.Parse("qq", Guid.Empty, envelope);
    }
}
