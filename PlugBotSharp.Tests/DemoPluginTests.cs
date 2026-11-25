using DemoPlugin;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.QQLib;

namespace PlugBotSharp.Tests;

/// <summary>
/// DemoPlugin 的示例命令，顺带验证各种 SendGroupMessage 消息体
/// </summary>
public class DemoPluginTests : ComponentTestBase
{
    [Fact]
    public void Text_command_returns_text_message()
    {
        var reply = Assert.Single(Send("/text"));

        Assert.Equal("这是一条文本消息", Assert.IsType<TextMessage>(reply).Text);
    }

    [Fact]
    public void Text_command_uses_the_saved_config()
    {
        var plugin = new PluginMain();

        plugin.UpdateMap(new Dictionary<string, ConfigMap>
        {
            ["Text"] = new ConfigStringMap { Key = "Text", Value = "改过的内容" }
        });

        var reply = Assert.Single(Send("/text", plugin));

        Assert.Equal("改过的内容", Assert.IsType<TextMessage>(reply).Text);
    }

    [Fact]
    public void Markdown_command_sends_msg_type_2()
    {
        var obj = ToQQ(Assert.Single(Send("/markdown")));

        Assert.Equal(2, obj.MsgType);
        Assert.Contains("DemoPlugin", obj.Markdown!.Content);
    }

    [Fact]
    public void Keyboard_command_sends_buttons()
    {
        var obj = ToQQ(Assert.Single(Send("/keyboard")));

        var rows = obj.Keyboard!.Content!.Rows;
        var buttons = Assert.Single(rows).Buttons;

        Assert.Equal(2, buttons.Count);
        Assert.Equal(ActionType.Command, buttons[0].Action.Type);
        Assert.Equal("/text", buttons[0].Action.Data);
        Assert.Equal(ActionType.JumpHttp, buttons[1].Action.Type);
    }

    [Fact]
    public void Card_command_sends_a_tuwen_card()
    {
        var obj = ToQQ(Assert.Single(Send("/card")));

        Assert.Equal(3, obj.MsgType);
        Assert.Equal("tuwen", obj.Card!.Type);
        Assert.Equal("DemoPlugin", obj.Card.Content.Title);
    }

    [Fact]
    public void List_command_returns_several_messages()
    {
        var reply = Assert.Single(Send("/list"));

        var list = Assert.IsType<ListMessage>(reply);
        Assert.Equal(2, list.Messages.Count);
    }

    [Fact]
    public void Reply_command_quotes_the_incoming_message()
    {
        var reply = Assert.Single(Send("/reply", replyId: "88"));
        var list = Assert.IsType<ListMessage>(reply);

        Assert.Equal("88", Assert.IsType<ReplyMessage>(list.Messages[0]).MessageId);
    }

    [Fact]
    public void Unknown_command_is_ignored()
    {
        Assert.Empty(Send("/nothing"));
    }

    /// <summary>
    /// 发一条命令，返回插件要回复的消息
    /// </summary>
    private static List<BaseMessage> Send(string text, PluginMain? plugin = null, string? replyId = null)
    {
        plugin ??= new PluginMain();
        plugin.RegisterEventHook();

        try
        {
            return EventManager.HandleMessage(new ProxyMessage
            {
                Id = "qq",
                Uuid = Guid.Empty,
                SenderType = MessageSenderType.Group,
                Sender = "user",
                ReplyId = replyId,
                Message = new TextMessage(text)
            });
        }
        finally
        {
            EventManager.Unregister(plugin);
        }
    }

    /// <summary>
    /// 按 QQ 代理的方式把回复转成平台消息体
    /// </summary>
    private static SendGroupMessageObj ToQQ(BaseMessage message)
    {
        return QQMessage.From(message)!;
    }
}