using System.Text.Json;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.OneBotLib;
using PlugBotSharp.OneBotProxy;

namespace PlugBotSharp.Tests;

/// <summary>
/// OneBot 12 的事件和消息段
/// </summary>
public class OneBotV12Tests
{
    [Fact]
    public void Group_message_event_becomes_a_proxy_message()
    {
        var message = Parse("""
            {
              "id": "event-1", "time": 1700000000, "type": "message", "detail_type": "group", "sub_type": "",
              "self": { "platform": "qq", "user_id": "10000" },
              "message_id": "42", "user_id": "20000", "group_id": "30000",
              "message": [
                { "type": "text", "data": { "text": "/text " } },
                { "type": "mention", "data": { "user_id": "999" } }
              ],
              "alt_message": "/text @999",
              "sender": { "user_id": "20000", "nickname": "tester" }
            }
            """)!;

        Assert.Equal(MessageSenderType.Group, message.SenderType);
        Assert.Equal("20000", message.Sender);
        Assert.Equal("30000", message.Target);
        Assert.Equal("42", message.ReplyId);

        var list = Assert.IsType<ListMessage>(message.Message);
        Assert.Equal("/text ", Assert.IsType<TextMessage>(list.Messages[0]).Text);
        Assert.Equal("999", Assert.IsType<MentionMessage>(list.Messages[1]).UserId);
    }

    [Fact]
    public void Private_message_event_has_no_target()
    {
        var message = Parse("""
            {
              "type": "message", "detail_type": "private", "self": { "platform": "qq", "user_id": "10000" },
              "message_id": "7", "user_id": "20000",
              "message": [ { "type": "text", "data": { "text": "hi" } } ]
            }
            """)!;

        Assert.Equal(MessageSenderType.User, message.SenderType);
        Assert.Equal("20000", message.Sender);
        Assert.Equal("", message.Target);
    }

    [Fact]
    public void Mention_all_segment_is_understood()
    {
        var body = OneBotMessage.FromSegments(JsonDocument.Parse("""
            [ { "type": "mention_all", "data": {} }, { "type": "text", "data": { "text": "hi" } } ]
            """).RootElement, out _, OneBotVersion.V12);

        var list = Assert.IsType<ListMessage>(body);
        var mention = Assert.Single(list.Messages.OfType<MentionMessage>());

        Assert.True(mention.Everyone);
        Assert.Equal("hi", Assert.IsType<TextMessage>(list.Messages[0]).Text);
    }

    [Fact]
    public void Mention_becomes_mention_segment_in_v12()
    {
        var segments = OneBotMessage.ToSegments(new MentionMessage("999"), OneBotVersion.V12);

        Assert.Equal("mention", Assert.Single(segments)!["type"]!.GetValue<string>());
        Assert.Equal("999", segments[0]!["data"]!["user_id"]!.GetValue<string>());
    }

    [Fact]
    public void Mention_all_becomes_mention_all_in_v12()
    {
        var segments = OneBotMessage.ToSegments(new MentionMessage { Everyone = true }, OneBotVersion.V12);

        Assert.Equal("mention_all", Assert.Single(segments)!["type"]!.GetValue<string>());
    }

    [Fact]
    public void V11_still_uses_at_segment()
    {
        var segments = OneBotMessage.ToSegments(new MentionMessage { Everyone = true }, OneBotVersion.V11);

        Assert.Equal("at", Assert.Single(segments)!["type"]!.GetValue<string>());
        Assert.Equal("all", segments[0]!["data"]!["qq"]!.GetValue<string>());
    }

    [Fact]
    public void V12_send_uses_send_message_action()
    {
        var (action, param) = OneBotAction.SendMessage(OneBotVersion.V12, true, "30000",
            OneBotMessage.ToSegments(new TextMessage("hi"), OneBotVersion.V12));

        Assert.Equal("send_message", action);
        Assert.Equal("group", param["detail_type"]!.GetValue<string>());
        Assert.Equal("30000", param["group_id"]!.GetValue<string>());
        Assert.Equal("text", param["message"]!.AsArray()[0]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void V11_send_uses_send_group_msg_action()
    {
        var (action, param) = OneBotAction.SendMessage(OneBotVersion.V11, true, "30000",
            OneBotMessage.ToSegments(new TextMessage("hi"), OneBotVersion.V11));

        Assert.Equal("send_group_msg", action);
        Assert.Equal(30000, param["group_id"]!.GetValue<long>());
        Assert.False(param["auto_escape"]!.GetValue<bool>());
    }

    /// <summary>
    /// 解析一条 v12 上报事件
    /// </summary>
    private static ProxyMessage? Parse(string json)
    {
        return OneBotEvent.Parse("onebot", Guid.Empty, JsonDocument.Parse(json).RootElement, OneBotVersion.V12);
    }
}