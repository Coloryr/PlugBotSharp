using System.Text;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.GsCoreProxy;

namespace PlugBotSharp.Tests;

/// <summary>
/// gsuid_core 下发消息的解析和转换
/// </summary>
public class GenshinReceiveTests
{
    [Fact]
    public void Text_and_reply_segments_become_messages()
    {
        var list = GenshinMessage.FromGs(
        [
            GsMessage.Of("reply_id", "100"),
            GsMessage.Of("text", "hello"),
            GsMessage.Of("at", "all")
        ], out var replyId);

        Assert.Equal("100", replyId);
        Assert.Equal([MessageType.Reply, MessageType.Text, MessageType.Mention], list.Select(x => x.Type).ToArray());
        Assert.True(Assert.IsType<MentionMessage>(list[2]).Everyone);
    }

    [Fact]
    public void Link_image_becomes_a_url_message()
    {
        var image = Assert.Single(GenshinMessage.FromGs([GsMessage.Of("image", "link://https://a/b.png")], out _));

        Assert.Equal("https://a/b.png", Assert.IsType<ImageUrlMessage>(image).Url);
    }

    [Fact]
    public void Base64_image_is_read_into_memory()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var segment = GsMessage.Of("image", "base64://" + Convert.ToBase64String(bytes));

        var image = Assert.IsType<ImageMessage>(Assert.Single(GenshinMessage.FromGs([segment], out _)));

        Assert.Equal(bytes, image.Image.ToArray());
    }

    [Fact]
    public void Unknown_segment_is_passed_through()
    {
        var raw = Assert.IsType<RawMessage>(
            Assert.Single(GenshinMessage.FromGs([GsMessage.Of("buttons", "x")], out _)));

        Assert.Equal(GenshinMessage.SegmentName, raw.Name);
        Assert.Equal("buttons", raw.Payload["type"]!.GetValue<string>());
    }

    [Fact]
    public void Group_message_becomes_a_proxy_message()
    {
        var uuid = Guid.NewGuid();
        var instance = new GenshinInstance("gscoreproxy");

        instance.Init(Path.GetTempPath(), uuid);

        var message = instance.BuildMessage(new GsMessageReceive
        {
            MsgId = "42",
            UserType = "group",
            GroupId = "30000",
            UserId = "20000",
            Content = [GsMessage.Of("text", "hi")]
        });

        Assert.NotNull(message);
        Assert.Equal("gscoreproxy", message!.Id);
        Assert.Equal(uuid, message.Uuid);
        Assert.Equal(MessageSenderType.Group, message.SenderType);
        Assert.Equal("20000", message.Sender);
        Assert.Equal("30000", message.Target);
        Assert.Equal("42", message.ReplyId);
        Assert.Equal("hi", Assert.IsType<TextMessage>(message.Message).Text);
    }

    [Fact]
    public void Received_message_is_dispatched_to_subscribers()
    {
        var uuid = Guid.NewGuid();
        var instance = new GenshinInstance("gscoreproxy");

        instance.Init(Path.GetTempPath(), uuid);
        instance.Load();
        instance.RegisterEventHook(new ComponentRegister(instance, "gscoreproxy", "gscoreproxy", uuid));

        var plugin = new object();
        ProxyMessage? received = null;

        EventManager.SubscribeMessage(plugin, message =>
        {
            received = message;

            return [];
        });

        try
        {
            instance.OnReceived(new GsMessageReceive
            {
                MsgId = "9",
                UserType = "group",
                GroupId = "30000",
                UserId = "20000",
                Content = [GsMessage.Of("text", "hi")]
            });

            Assert.NotNull(received);
            Assert.Equal("gscoreproxy", received!.Id);
            Assert.Equal(uuid, received.Uuid);
            Assert.Equal("hi", Assert.IsType<TextMessage>(received.Message).Text);
            Assert.Equal(1L, StateRegistry.Get(instance)["recv"].Value);
        }
        finally
        {
            EventManager.Unregister(instance);
            EventManager.Unregister(plugin);
        }
    }

    [Fact]
    public void Reply_is_routed_back_to_the_origin_instance()
    {
        var uuid = Guid.NewGuid();
        var instance = new GenshinInstance("gscoreproxy");

        instance.Init(Path.GetTempPath(), uuid);
        instance.Load();
        instance.RegisterEventHook(new ComponentRegister(instance, "gscoreproxy", "gscoreproxy", uuid));

        var target = new object();
        ProxyMessage? sent = null;
        BaseMessage? body = null;

        try
        {
            EventManager.RegisterSendTarget(target, (ctx, msg) =>
            {
                sent = ctx;
                body = msg;

                return Task.CompletedTask;
            });

            var originUuid = Guid.NewGuid();

            // 别的实例发一条消息，GsCore 转给 core 并记住路由
            EventManager.HandleMessage(new ProxyMessage
            {
                Id = "qqproxy",
                Uuid = originUuid,
                SenderType = MessageSenderType.Group,
                Sender = "20000",
                Target = "30000",
                Message = new TextMessage("ww帮助"),
                ReplyId = "1001"
            });

            // core 回一张图，msg_id 带的是刚才那条消息的 id
            instance.OnReceived(new GsMessageReceive
            {
                MsgId = "1001",
                TargetType = "group",
                TargetId = "30000",
                Content = [GsMessage.Of("image", "base64://" + Convert.ToBase64String([1, 2, 3]))]
            });

            Assert.NotNull(sent);
            Assert.Equal("qqproxy", sent!.Id);
            Assert.Equal(originUuid, sent.Uuid);
            Assert.Equal("30000", sent.Target);
            Assert.Equal("20000", sent.Sender);
            Assert.IsType<ImageMessage>(body);
        }
        finally
        {
            EventManager.Unregister(target);
            EventManager.Unregister(instance);
        }
    }

    [Fact]
    public void Empty_content_is_ignored()
    {
        var instance = new GenshinInstance("gscoreproxy");

        instance.Init(Path.GetTempPath(), Guid.NewGuid());

        Assert.Null(instance.BuildMessage(new GsMessageReceive { UserType = "direct", Content = [] }));
    }

    [Fact]
    public void Client_parses_the_frame_and_raises_the_event()
    {
        using var client = new GenshinClient();

        GsMessageReceive? received = null;

        client.MessageReceived += message => received = message;

        client.HandleFrame(Encoding.UTF8.GetBytes("""
            {
              "bot_id": "QQ",
              "bot_self_id": "10000",
              "msg_id": "7",
              "user_type": "group",
              "group_id": "30000",
              "user_id": "20000",
              "content": [{ "type": "text", "data": "hi" }]
            }
            """));

        Assert.NotNull(received);
        Assert.Equal("QQ", received!.BotId);
        Assert.Equal("7", received.MsgId);
        Assert.Equal("hi", received.Content[0].Data!.GetValue<string>());
    }

    [Fact]
    public void Broken_frame_is_ignored()
    {
        using var client = new GenshinClient();

        var raised = false;

        client.MessageReceived += _ => raised = true;

        client.HandleFrame(Encoding.UTF8.GetBytes("{ not json"));

        Assert.False(raised);
    }
}
