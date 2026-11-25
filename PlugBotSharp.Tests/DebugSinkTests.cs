using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;
using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.Tests;

/// <summary>
/// 面板调试终端：截获发往调试 uuid 的回复，标出来源，列表拆成多条
/// </summary>
public class DebugSinkTests : ComponentTestBase
{
    [Fact]
    public async Task Replies_to_the_debug_terminal_are_captured_with_their_source()
    {
        DebugSink.Clear();

        var context = new ProxyMessage
        {
            Id = "debug",
            Uuid = DebugSink.Uuid,
            SenderType = MessageSenderType.Group,
            Sender = "u1",
            Target = "g1",
            Message = new TextMessage("/test")
        };

        await EventManager.HandleSendAsync(context, new TextMessage("来自插件"), "demo");

        var item = Assert.Single(DebugSink.Since(0));

        Assert.Equal("Text", item.Type);
        Assert.Equal("来自插件", item.Text);
        Assert.Equal("demo", item.Source);
    }

    [Fact]
    public async Task List_replies_are_split_into_separate_captures()
    {
        DebugSink.Clear();

        var context = new ProxyMessage
        {
            Id = "debug",
            Uuid = DebugSink.Uuid,
            SenderType = MessageSenderType.Group,
            Sender = "u1",
            Target = "g1",
            Message = new TextMessage("/test")
        };

        var list = new ListMessage(new TextMessage("第一段"), new TextMessage("第二段"));

        await EventManager.HandleSendAsync(context, list, "gscoreproxy/9e4aaf8b");

        var items = DebugSink.Since(0);

        Assert.Equal(2, items.Count);
        Assert.Equal(["第一段", "第二段"], items.Select(x => x.Text));
        Assert.All(items, x => Assert.Equal("gscoreproxy/9e4aaf8b", x.Source));
    }

    [Fact]
    public async Task Images_come_with_a_preview_mime()
    {
        DebugSink.Clear();

        var context = new ProxyMessage
        {
            Id = "debug",
            Uuid = DebugSink.Uuid,
            SenderType = MessageSenderType.User,
            Sender = "u1",
            Message = new TextMessage("/pic")
        };

        using var stream = new MemoryStream([1, 2, 3, 4]);
        var image = new ImageMessage(stream, ImageType.Png);

        await EventManager.HandleSendAsync(context, new ListMessage(new TextMessage("看图"), image), "demo");

        var items = DebugSink.Since(0);

        Assert.Equal(2, items.Count);
        Assert.Null(items[0].PreviewMime);
        Assert.Equal("image/png", items[1].PreviewMime);
        Assert.NotNull(DebugSink.Image(items[1].Seq));
    }
}
