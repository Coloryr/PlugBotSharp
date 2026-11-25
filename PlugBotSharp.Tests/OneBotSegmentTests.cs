using PlugBotSharp.Core.Messages;
using PlugBotSharp.OneBotLib;

namespace PlugBotSharp.Tests;

/// <summary>
/// OneBot 专属消息段
/// </summary>
public class OneBotSegmentTests
{
    [Fact]
    public void Music_segment_drops_null_fields()
    {
        var segment = OneBotSegment.Music("qq", "123");

        Assert.Equal("music", segment["type"]!.GetValue<string>());

        var data = segment["data"]!.AsObject();
        Assert.Equal("qq", data["type"]!.GetValue<string>());
        Assert.Equal("123", data["id"]!.GetValue<string>());
        Assert.False(data.ContainsKey("url"));
    }

    [Fact]
    public void Contact_segment_uses_the_right_type()
    {
        Assert.Equal("group", OneBotSegment.Contact(true, "1")["data"]!["type"]!.GetValue<string>());
        Assert.Equal("qq", OneBotSegment.Contact(false, "1")["data"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Raw_wraps_the_segment()
    {
        var raw = OneBotSegment.Raw(OneBotSegment.Location(39.9, 116.4, "北京"));

        Assert.Equal(OneBotSegment.Name, raw.Name);
        Assert.Equal("location", raw.Payload["type"]!.GetValue<string>());
    }

    [Fact]
    public void Raw_segment_is_sent_as_is()
    {
        var raw = OneBotSegment.Raw(OneBotSegment.Dice(3));
        var segments = OneBotMessage.ToSegments(raw);

        Assert.Equal("dice", Assert.Single(segments)!["type"]!.GetValue<string>());
        Assert.Equal(3, segments[0]!["data"]!["result"]!.GetValue<int>());
    }

    [Fact]
    public void Shake_segment_has_empty_data()
    {
        var segment = OneBotSegment.Shake();

        Assert.Equal("shake", segment["type"]!.GetValue<string>());
        Assert.Empty(segment["data"]!.AsObject());
    }
}
