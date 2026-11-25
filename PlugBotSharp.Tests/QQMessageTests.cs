using System.Text.Json;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.QQLib;

namespace PlugBotSharp.Tests;

/// <summary>
/// 通用消息到 QQ 消息体的转换
/// </summary>
public class QQMessageTests
{
    [Fact]
    public void Text_message_maps_to_msg_type_0()
    {
        var obj = QQMessage.From(new TextMessage("hi"));

        Assert.NotNull(obj);
        Assert.Equal(0, obj!.MsgType);
        Assert.Equal("hi", obj.Content);
    }

    [Fact]
    public void Raw_message_passes_through()
    {
        var payload = new SendGroupMessageObj
        {
            MsgType = 2,
            Markdown = new MessageMarkdownObj { Content = "# hi" }
        };

        var node = JsonSerializer.SerializeToNode(payload, QQJsonContext.Default.SendGroupMessageObj)!;
        var obj = QQMessage.From(new RawMessage(QQMessage.SendName, node));

        Assert.NotNull(obj);
        Assert.Equal(2, obj!.MsgType);
        Assert.Equal("# hi", obj.Markdown!.Content);
    }

    [Fact]
    public void Markdown_message_maps_to_msg_type_2()
    {
        var obj = QQMessage.From(new MarkdownMessage("**hi**"));

        Assert.NotNull(obj);
        Assert.Equal(2, obj!.MsgType);
        Assert.Equal("**hi**", obj.Markdown!.Content);
    }

    [Fact]
    public void Reply_message_becomes_a_message_reference()
    {
        var obj = QQMessage.From(new ListMessage(new ReplyMessage("42"), new TextMessage("hi")), "99");

        Assert.NotNull(obj);
        Assert.Equal("hi", obj!.Content);
        Assert.Equal("42", obj.MessageReference!.MessageId);
        Assert.Equal("99", obj.MsgId);
    }

    [Fact]
    public void Unknown_raw_name_is_not_converted()
    {
        var node = JsonSerializer.SerializeToNode(new SendGroupMessageObj { MsgType = 0 })!;

        Assert.Null(QQMessage.From(new RawMessage("other.send", node)));
    }

    [Fact]
    public void Unsupported_message_returns_null()
    {
        Assert.Null(QQMessage.From(new ImageUrlMessage("https://example.com/a.png", ImageType.Png)));
    }

    [Fact]
    public void Reply_context_is_attached()
    {
        var obj = QQMessage.From(new TextMessage("hi"), "msg-1", "event-1", 3);

        Assert.NotNull(obj);
        Assert.Equal("msg-1", obj!.MsgId);
        Assert.Equal("event-1", obj.EventId);
        Assert.Equal(3, obj.MsgSeq);
    }
}