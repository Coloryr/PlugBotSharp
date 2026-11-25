using PlugBotSharp.Core;

namespace PlugBotSharp.Tests;

/// <summary>
/// 自定义事件名 [组件id]事件id 的拼装和解析
/// </summary>
public class PluginEventNameTests
{
    [Fact]
    public void Build_wraps_the_component_id_in_brackets()
    {
        Assert.Equal("[demo]ping", PluginEventName.Build("demo", "ping"));
    }

    [Fact]
    public void TryParse_splits_the_name_back()
    {
        Assert.True(PluginEventName.TryParse("[demo]ping", out var id, out var eventId));
        Assert.Equal("demo", id);
        Assert.Equal("ping", eventId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("demo.ping")]
    [InlineData("[]ping")]
    [InlineData("[demo]")]
    public void TryParse_rejects_malformed_names(string name)
    {
        Assert.False(PluginEventName.TryParse(name, out _, out _));
    }

    [Fact]
    public void Build_rejects_brackets_inside_the_ids()
    {
        Assert.Throws<ArgumentException>(() => PluginEventName.Build("de[mo", "ping"));
        Assert.Throws<ArgumentException>(() => PluginEventName.Build("demo", "pi]ng"));
    }
}
