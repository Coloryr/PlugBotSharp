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

    /// <summary>
    /// 已经回复过多少条，作为自定义状态上报给面板
    /// </summary>
    private int _replies;

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

    /// <summary>
    /// 自定义事件 [demo]ping
    /// </summary>
    private RaiseHandle? _ping;

    /// <summary>
    /// 收到插件事件的次数（生命周期 + 自定义），作为状态上报
    /// </summary>
    private int _events;

    /// <summary>
    /// 收到自定义事件 [demo]ping 的次数，作为状态上报
    /// </summary>
    private int _pings;

    /// <summary>
    /// 状态句柄，注册阶段拿到，之后随时更新
    /// </summary>
    private StateHandle? _pingState;

    private StateHandle? _eventState;

    private StateHandle? _replyState;

    private StateHandle? _blockedState;

    /// <summary>
    /// 被拦截器拦下的消息条数
    /// </summary>
    private int _blocked;

    /// <inheritdoc/>
    public void RegisterEventHook(IRegister register)
    {
        register.SubscribeMessage(HandleMessage);

        // 注册自定义事件，名字是 [demo]ping
        _ping = register.RegisterPluginEvent("ping");

        // 注册状态，句柄留着随时更新
        _pingState = register.RegisterState("pings");
        _eventState = register.RegisterState("events");
        _replyState = register.RegisterState("replies");
        _blockedState = register.RegisterState("blocked");

        // 发送前拦截：返回 true 就把这条消息丢掉，不发给真实平台
        register.SubscribeSendFilter((_, reply) =>
        {
            if (reply is TextMessage text && text.Text.Contains("拦截"))
            {
                _blocked++;
                _blockedState?.Set(_blocked.ToString());

                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        });

        // 定向订阅：只收 [demo]ping，直接用注册时拿到的全名，不写死字符串
        register.SubscribePluginEvent(_ping.Name, e =>
        {
            _pings++;
            _pingState?.Set(_pings.ToString());
        });

        // 广播订阅：生命周期事件和所有自定义事件都收
        register.SubscribePlugin(e =>
        {
            _events++;
            _eventState?.Set(_events.ToString());
        });
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

        var replies = text.Text.Trim().ToLowerInvariant() switch
        {
            "/text" => [new TextMessage(_text)],
            "/markdown" => [new MarkdownMessage("**DemoPlugin**\n> 这是一条 markdown 消息")],
            "/keyboard" => [ToRaw(BuildKeyboard())],
            "/card" => [ToRaw(BuildCard())],
            "/list" => [new ListMessage(new TextMessage("第一条"), new TextMessage("第二条"))],
            "/reply" => BuildReply(message),
            "/event" => BuildEvent(),
            "/block" => [new TextMessage("这条消息会被拦截器拦下")],
            _ => []
        };

        if (replies.Count > 0)
        {
            _replies += replies.Count;
            _replyState?.Set(_replies.ToString());
        }

        return replies;
    }

    /// <summary>
    /// 触发自己的自定义事件
    /// </summary>
    private List<BaseMessage> BuildEvent()
    {
        // 事件数据只能是基础类型或 JsonNode，别传组件自己的对象
        _ping?.Raise("demo");

        return [new TextMessage("事件已触发")];
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