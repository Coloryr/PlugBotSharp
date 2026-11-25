using System.Text.Json;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.Core.Plugin;
using PlugBotSharp.QQLib;

namespace DemoPlugin;

/// <summary>
/// 示例插件：演示接收消息，以及用 RawMessage 发 QQ 的各种富消息
/// </summary>
public class PluginMain : IBasePlugin
{
    /// <summary>
    /// /text 回复的内容
    /// </summary>
    private string _text = "这是一条文本消息";

    /// <inheritdoc/>
    public bool Init(string path)
    {
        return true;
    }

    /// <inheritdoc/>
    public bool Load()
    {
        return true;
    }

    /// <inheritdoc/>
    public void Reload()
    {
    }

    /// <inheritdoc/>
    public void RegisterEventHook()
    {
        EventManager.RegisterMessageHook(this, HandleMessage);
    }

    /// <inheritdoc/>
    public bool Enable()
    {
        return true;
    }

    /// <inheritdoc/>
    public bool Disable()
    {
        return true;
    }

    /// <inheritdoc/>
    public bool Unload()
    {
        return true;
    }

    /// <inheritdoc/>
    public Dictionary<string, ConfigMap> ConfigMap()
    {
        return new()
        {
            ["Text"] = new ConfigStringMap
            {
                Key = "Text",
                Value = _text,
                Default = "这是一条文本消息",
                MaxLength = 200
            }
        };
    }

    /// <inheritdoc/>
    public void UpdateMap(Dictionary<string, ConfigMap> data)
    {
        if (data.TryGetValue("Text", out var item) && item is ConfigStringMap map)
        {
            _text = map.Value ?? "";
        }
    }

    /// <summary>
    /// 消息事件：只处理 / 开头的命令
    /// </summary>
    /// <param name="message">收到的消息</param>
    private List<BaseMessage> HandleMessage(ProxyMessage message)
    {
        if (message.Message is not TextMessage text)
        {
            return [];
        }

        return text.Text.Trim().ToLowerInvariant() switch
        {
            "/text" => [new TextMessage(_text)],
            "/markdown" => [new MarkdownMessage("**DemoPlugin**\n> 这是一条 markdown 消息")],
            "/keyboard" => [ToRaw(BuildKeyboard())],
            "/card" => [ToRaw(BuildCard())],
            "/list" => [new ListMessage(new TextMessage("第一条"), new TextMessage("第二条"))],
            "/reply" => BuildReply(message),
            _ => []
        };
    }

    /// <summary>
    /// 引用收到的消息回复
    /// </summary>
    private static List<BaseMessage> BuildReply(ProxyMessage message)
    {
        if (string.IsNullOrEmpty(message.ReplyId))
        {
            return [new TextMessage("这条消息没有 id，没法引用")];
        }

        return [new ListMessage(new ReplyMessage(message.ReplyId), new TextMessage("收到"))];
    }

    /// <summary>
    /// 平台消息体转成直通消息，代理按 QQMessage.SendName 认
    /// </summary>
    private static RawMessage ToRaw(SendGroupMessageObj obj)
    {
        return new RawMessage(QQMessage.SendName,
            JsonSerializer.SerializeToNode(obj, QQJsonContext.Default.SendGroupMessageObj)!);
    }

    /// <summary>
    /// 带内嵌键盘的消息，键盘只能跟着 markdown 一起发
    /// </summary>
    private static SendGroupMessageObj BuildKeyboard()
    {
        return new SendGroupMessageObj
        {
            MsgType = 2,
            Markdown = new MessageMarkdownObj
            {
                Content = "点下面的按钮试试"
            },
            Keyboard = new KeyboardObj
            {
                Content = new KeyboardContentObj
                {
                    Rows =
                    [
                        new RowObj
                        {
                            Buttons =
                            [
                                new ButtonObj
                                {
                                    Id = "1",
                                    RenderData = new RenderDataObj
                                    {
                                        Label = "发文本",
                                        VisitedLabel = "已点击",
                                        Style = RenderDataStyle.BlueBorder
                                    },
                                    Action = new ActionObj
                                    {
                                        Type = ActionType.Command,
                                        Data = "/text",
                                        Enter = true,
                                        UnsupportTips = "请升级客户端"
                                    }
                                },
                                new ButtonObj
                                {
                                    Id = "2",
                                    RenderData = new RenderDataObj
                                    {
                                        Label = "打开文档",
                                        VisitedLabel = "已打开",
                                        Style = RenderDataStyle.GrayBorder
                                    },
                                    Action = new ActionObj
                                    {
                                        Type = ActionType.JumpHttp,
                                        Data = "https://bot.q.qq.com/wiki/",
                                        UnsupportTips = "请升级客户端"
                                    }
                                }
                            ]
                        }
                    ]
                }
            }
        };
    }

    /// <summary>
    /// 图文卡片，msg_type 用 3
    /// </summary>
    private static SendGroupMessageObj BuildCard()
    {
        return new SendGroupMessageObj
        {
            MsgType = 3,
            Card = new MessageCardObj
            {
                Content = new CardObj
                {
                    Title = "DemoPlugin",
                    Description = "图文卡片示例",
                    Url = "https://bot.q.qq.com/wiki/",
                    PicUrl = "https://bot.q.qq.com/wiki/logo.png"
                }
            }
        };
    }
}