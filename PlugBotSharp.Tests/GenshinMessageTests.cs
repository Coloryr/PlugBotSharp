using System.Text.Json;
using System.Text.Json.Nodes;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.GsCoreProxy;

namespace PlugBotSharp.Tests;

/// <summary>
/// gsuid_core 协议的模型和消息段转换
/// </summary>
public class GenshinMessageTests
{
    [Fact]
    public void Text_message_becomes_a_text_segment()
    {
        var segments = GenshinMessage.ToGs(new TextMessage("hi"));

        var segment = Assert.Single(segments);
        Assert.Equal("text", segment.Type);
        Assert.Equal("hi", segment.Data!.GetValue<string>());
    }

    [Fact]
    public void List_message_is_flattened()
    {
        var segments = GenshinMessage.ToGs(new ListMessage(
            new TextMessage("hi "),
            new MentionMessage("123"),
            new ImageUrlMessage("https://example.com/a.png", ImageType.Png),
            new ReplyMessage("42")));

        Assert.Equal(new[] { "text", "at", "image", "reply_id" }, segments.Select(x => x.Type ?? "").ToArray());
        Assert.Equal("123", segments[1].Data!.GetValue<string>());
        Assert.Equal("link://https://example.com/a.png", segments[2].Data!.GetValue<string>());
        Assert.Equal("42", segments[3].Data!.GetValue<string>());
    }

    [Fact]
    public void Mention_everyone_uses_all()
    {
        var segments = GenshinMessage.ToGs(new MentionMessage { Everyone = true });

        Assert.Equal("all", Assert.Single(segments).Data!.GetValue<string>());
    }

    [Fact]
    public void Raw_segment_is_passed_through()
    {
        var raw = new RawMessage(GenshinMessage.SegmentName,
            JsonNode.Parse("""{ "type": "buttons", "data": "x" }"""));

        var segment = Assert.Single(GenshinMessage.ToGs(raw));

        Assert.Equal("buttons", segment.Type);
        Assert.Equal("x", segment.Data!.GetValue<string>());
    }

    [Fact]
    public void Receive_model_uses_snake_case()
    {
        var receive = new GsMessageReceive
        {
            BotId = "bot",
            BotSelfId = "10000",
            MsgId = "1",
            UserType = "group",
            GroupId = "30000",
            UserId = "20000",
            Content = [GsMessage.Of("text", "hi")]
        };

        var json = JsonSerializer.Serialize(receive, GenshinJsonContext.Default.GsMessageReceive);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("bot", node["bot_id"]!.GetValue<string>());
        Assert.Equal("10000", node["bot_self_id"]!.GetValue<string>());
        Assert.Equal("group", node["user_type"]!.GetValue<string>());
        Assert.Equal("30000", node["group_id"]!.GetValue<string>());
        Assert.Equal("text", node["content"]![0]!["type"]!.GetValue<string>());
    }
}
