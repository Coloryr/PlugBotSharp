using System.Text.Json;
using System.Text.Json.Nodes;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.OneBotLib;
using PlugBotSharp.OneBotProxy;

namespace PlugBotSharp.Tests;

/// <summary>
/// OneBot 11 事件和消息段的转换
/// </summary>
public class OneBotMessageTests
{
    [Fact]
    public void Group_message_event_becomes_a_proxy_message()
    {
        var message = Parse("""
            {
              "time": 1700000000, "self_id": 10000, "post_type": "message", "message_type": "group",
              "sub_type": "normal", "message_id": 42, "user_id": 20000, "group_id": 30000,
              "message": [ { "type": "text", "data": { "text": "/text" } } ],
              "raw_message": "/text", "font": 0,
              "sender": { "user_id": 20000, "nickname": "tester" }
            }
            """)!;

        Assert.Equal(MessageSenderType.Group, message.SenderType);
        Assert.Equal("20000", message.Sender);
        Assert.Equal("30000", message.Target);
        Assert.Equal("42", message.ReplyId);
        Assert.Equal("/text", Assert.IsType<TextMessage>(message.Message).Text);
    }

    [Fact]
    public void Private_message_event_has_no_target()
    {
        var message = Parse("""
            {
              "post_type": "message", "message_type": "private", "message_id": 7, "user_id": 20000,
              "message": [ { "type": "text", "data": { "text": "hi" } } ]
            }
            """)!;

        Assert.Equal(MessageSenderType.User, message.SenderType);
        Assert.Equal("20000", message.Sender);
        Assert.Equal("", message.Target);
    }

    [Fact]
    public void Mixed_segments_are_merged_into_a_list()
    {
        var message = Parse("""
            {
              "post_type": "message", "message_type": "group", "message_id": 1, "user_id": 20000, "group_id": 30000,
              "message": [
                { "type": "text", "data": { "text": "hello " } },
                { "type": "at", "data": { "qq": "12345" } },
                { "type": "face", "data": { "id": "1" } },
                { "type": "image", "data": { "url": "https://example.com/a.png" } }
              ]
            }
            """)!;

        var list = Assert.IsType<ListMessage>(message.Message);

        Assert.Equal(4, list.Messages.Count);
        Assert.Equal("hello ", Assert.IsType<TextMessage>(list.Messages[0]).Text);
        Assert.Equal("12345", Assert.IsType<MentionMessage>(list.Messages[1]).UserId);
        Assert.Equal("1", Assert.IsType<FaceMessage>(list.Messages[2]).Id);
        Assert.Equal(ImageType.Png, Assert.IsType<ImageUrlMessage>(list.Messages[3]).ImageType);
    }

    [Fact]
    public void Reply_segment_sets_the_reply_id()
    {
        var body = OneBotMessage.FromSegments(JsonDocument.Parse("""
            [ { "type": "reply", "data": { "id": "99" } }, { "type": "text", "data": { "text": "x" } } ]
            """).RootElement, out var replyId);

        Assert.Equal("99", replyId);
        Assert.Equal("x", Assert.IsType<TextMessage>(body).Text);
    }

    [Fact]
    public void Messages_are_converted_to_segments()
    {
        var segments = OneBotMessage.ToSegments(new ListMessage(
            new TextMessage("hi"),
            new MentionMessage { Everyone = true },
            new MentionMessage("12345"),
            new FaceMessage { Id = "1" },
            new ImageUrlMessage("https://example.com/a.png", ImageType.Png),
            new CardMessage { Data = "{}" }
        ));

        Assert.Equal(6, segments.Count);
        Assert.Equal("text", segments[0]!["type"]!.GetValue<string>());
        Assert.Equal("all", segments[1]!["data"]!["qq"]!.GetValue<string>());
        Assert.Equal("12345", segments[2]!["data"]!["qq"]!.GetValue<string>());
        Assert.Equal("face", segments[3]!["type"]!.GetValue<string>());
        Assert.Equal("https://example.com/a.png", segments[4]!["data"]!["file"]!.GetValue<string>());
        Assert.Equal("json", segments[5]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Markdown_segment_becomes_markdown_message()
    {
        var body = OneBotMessage.FromSegments(JsonDocument.Parse("""
            [ { "type": "markdown", "data": { "content": "**hi**" } } ]
            """).RootElement, out _);

        Assert.Equal("**hi**", Assert.IsType<MarkdownMessage>(body).Content);
    }

    [Fact]
    public void Markdown_message_becomes_markdown_segment()
    {
        var segments = OneBotMessage.ToSegments(new MarkdownMessage("**hi**"));

        Assert.Equal("markdown", Assert.Single(segments)!["type"]!.GetValue<string>());
        Assert.Equal("**hi**", segments[0]!["data"]!["content"]!.GetValue<string>());
    }

    [Fact]
    public void Raw_segment_is_passed_through()
    {
        var raw = new RawMessage(OneBotMessage.SegmentName, JsonNode.Parse("""
            { "type": "record", "data": { "file": "a.amr" } }
            """));

        var segments = OneBotMessage.ToSegments(raw);

        Assert.Equal("record", Assert.Single(segments)!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Reply_message_becomes_a_reply_segment()
    {
        var segments = OneBotMessage.ToSegments(new ListMessage(new ReplyMessage("42"), new TextMessage("hi")));

        Assert.Equal("reply", segments[0]!["type"]!.GetValue<string>());
        Assert.Equal("42", segments[0]!["data"]!["id"]!.GetValue<string>());
        Assert.Equal("text", segments[1]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Auto_reply_is_not_added_twice()
    {
        var segments = OneBotMessage.WithReply(OneBotMessage.ToSegments(new ReplyMessage("42")), "99");

        Assert.Single(segments);
        Assert.Equal("42", segments[0]!["data"]!["id"]!.GetValue<string>());
    }

    [Fact]
    public void Reply_segment_is_prepended()
    {
        var segments = OneBotMessage.WithReply(OneBotMessage.ToSegments(new TextMessage("hi")), "42");

        Assert.Equal(2, segments.Count);
        Assert.Equal("reply", segments[0]!["type"]!.GetValue<string>());
        Assert.Equal("42", segments[0]!["data"]!["id"]!.GetValue<string>());
    }

    /// <summary>
    /// 解析一条上报事件
    /// </summary>
    private static ProxyMessage? Parse(string json)
    {
        return OneBotEvent.Parse("onebot", Guid.Empty, JsonDocument.Parse(json).RootElement);
    }
}