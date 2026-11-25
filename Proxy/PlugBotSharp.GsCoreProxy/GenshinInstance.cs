using System.Collections.Concurrent;
using System.Text.Json;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.Core.Proxy;

namespace PlugBotSharp.GsCoreProxy;

/// <summary>
/// gsuid_core 实例：把平台消息转给 core，同时接收 core 下发的消息
/// </summary>
public class GenshinInstance : IProxyInstasnce
{
    private readonly string _proxyId;

    private string _configFile = "";
    private InstanceDataObj _data = new();
    private Guid _uuid;

    private GenshinClient? _client;

    /// <summary>
    /// 代理事件句柄，core 下发的消息从这里注入
    /// </summary>
    private ProxyEventHandle? _event;

    /// <summary>
    /// 发送句柄，回复先过拦截器再发回 core
    /// </summary>
    private SendHandle? _send;

    private bool _enabled;
    private DateTime _lastTry;

    /// <summary>
    /// 收到 core 下发的消息条数，作为状态上报
    /// </summary>
    private int _received;

    /// <summary>
    /// 收到条数的状态句柄
    /// </summary>
    private StateValueHandle? _receiveState;

    /// <summary>
    /// 发出去的消息 id -> 发起的实例，core 的回复会带回同一个 msg_id
    /// </summary>
    private readonly ConcurrentDictionary<string, ProxyMessage> _msgRoutes = new();

    /// <summary>
    /// 群 / 用户 -> 发起的实例，msg_id 对不上时按会话兜底
    /// </summary>
    private readonly ConcurrentDictionary<string, ProxyMessage> _chatRoutes = new();

    /// <summary>
    /// 路由表最多留这么多条，防止一直涨
    /// </summary>
    private const int RouteLimit = 256;

    /// <summary>
    /// 创建实例
    /// </summary>
    /// <param name="proxyId">代理组件 id</param>
    public GenshinInstance(string proxyId)
    {
        _proxyId = proxyId;
    }

    /// <summary>
    /// 所属代理组件 id
    /// </summary>
    internal string ProxyId => _proxyId;

    /// <summary>
    /// 实例 uuid
    /// </summary>
    internal Guid Uuid => _uuid;

    /// <inheritdoc/>
    public void Init(string path, Guid uuid)
    {
        _configFile = Path.Combine(path, "config.json");
        _uuid = uuid;
    }

    /// <inheritdoc/>
    public bool Load()
    {
        return ReadConfig();
    }

    /// <inheritdoc/>
    public void Reload()
    {
        ReadConfig();
    }

    /// <inheritdoc/>
    public void RegisterEventHook(IRegister register)
    {
        // 订阅所有平台的消息，转给 gsuid_core
        register.SubscribeMessage(Forward);

        // core 下发的消息注入给插件，回复走发送句柄
        _event = register.RegisterProxyEvent();
        _send = register.RegisterSend();

        // 收到 core 下发的消息条数
        _receiveState = register.RegisterState("recv", "收到条数", "gsuid_core 下发的消息条数");
    }

    /// <inheritdoc/>
    public bool Enable()
    {
        if (!Uri.TryCreate(_data.Url, UriKind.Absolute, out var url))
        {
            GenshinLang.Log.Error(GenshinLang.Get(GenshinLangKey.UrlInvalid, _data.Url));
            return false;
        }

        _client ??= CreateClient();
        _enabled = true;

        Connect(url);

        return true;
    }

    /// <inheritdoc/>
    public bool Disable()
    {
        _enabled = false;

        try
        {
            _client?.StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // 断开失败不影响停用
        }

        return true;
    }

    /// <inheritdoc/>
    public bool Unload()
    {
        _enabled = false;

        _client?.Dispose();
        _client = null;

        return true;
    }

    /// <inheritdoc/>
    public void Tick()
    {
        if (!_enabled || _client is null || _client.Connected)
        {
            return;
        }

        if ((DateTime.Now - _lastTry).TotalSeconds < Math.Max(3, _data.ReconnectSeconds))
        {
            return;
        }

        if (Uri.TryCreate(_data.Url, UriKind.Absolute, out var url))
        {
            Connect(url);
        }
    }

    /// <inheritdoc/>
    public Dictionary<string, ConfigMap> ConfigMap()
    {
        return new()
        {
            ["Url"] = new ConfigStringMap
            {
                Key = "Url",
                Value = _data.Url,
                Default = "ws://127.0.0.1:8765",
                MaxLength = 200,
                Description = "gsuid_core 的地址，实际连的是 {Url}/ws/{BotId}",
                Placeholder = "ws://127.0.0.1:8765"
            },
            ["BotId"] = new ConfigStringMap
            {
                Key = "BotId",
                Value = _data.BotId,
                Default = "PlugBotSharp",
                MaxLength = 64,
                Description = "连接用的 bot_id，core 靠它区分不同适配器",
                Placeholder = "PlugBotSharp"
            },
            ["Token"] = new ConfigStringMap
            {
                Key = "Token",
                Value = _data.Token,
                Secret = true,
                Description = "core 配置的 ws token，没设就留空",
                Placeholder = "ws token"
            },
            ["ReconnectSeconds"] = new ConfigIntMap
            {
                Key = "ReconnectSeconds",
                Value = _data.ReconnectSeconds,
                Min = 3,
                Max = 600,
                Default = 5,
                Description = "断线后多少秒重连",
                Placeholder = "5"
            },
            ["CommandStart"] = new ConfigListMap
            {
                Key = "CommandStart",
                Value = [.. _data.CommandStart],
                Default = ["/"],
                MaxCount = 10,
                Description = "转发给 core 前剥掉的前缀，一行一个；core 的 command_start 配了前缀就留空"
            }
        };
    }

    /// <inheritdoc/>
    public void UpdateMap(Dictionary<string, ConfigMap> data)
    {
        if (data.TryGetValue("Url", out var url) && url is ConfigStringMap urlMap)
        {
            _data.Url = urlMap.Value ?? "";
        }

        if (data.TryGetValue("BotId", out var botId) && botId is ConfigStringMap botIdMap)
        {
            _data.BotId = botIdMap.Value ?? "";
        }

        if (data.TryGetValue("Token", out var token) && token is ConfigStringMap tokenMap)
        {
            _data.Token = tokenMap.Value ?? "";
        }

        if (data.TryGetValue("ReconnectSeconds", out var reconnect) && reconnect is ConfigIntMap reconnectMap)
        {
            _data.ReconnectSeconds = Math.Clamp(reconnectMap.Value, 3, 600);
        }

        if (data.TryGetValue("CommandStart", out var commandStart) && commandStart is ConfigListMap commandStartMap)
        {
            _data.CommandStart = [.. commandStartMap.Value.Where(x => !string.IsNullOrEmpty(x))];
        }

        SaveConfig();
    }

    /// <summary>
    /// core 下发的消息转成统一的消息事件
    /// </summary>
    /// <param name="receive">core 的消息</param>
    internal ProxyMessage? BuildMessage(GsMessageReceive receive)
    {
        var content = GenshinMessage.FromGs(receive.Content, out _);

        if (content.Count == 0)
        {
            return null;
        }

        // core 发出来的消息没有 user_type，用 target_type 标目标
        var userType = receive.TargetType ?? receive.UserType;

        var senderType = userType switch
        {
            "group" => MessageSenderType.Group,
            "channel" or "sub_channel" => MessageSenderType.Guild,
            _ => MessageSenderType.User
        };

        var target = !string.IsNullOrEmpty(receive.GroupId)
            ? receive.GroupId
            : userType is "group" or "channel" or "sub_channel" ? receive.TargetId ?? "" : "";

        return new ProxyMessage
        {
            Id = _proxyId,
            Uuid = _uuid,
            SenderType = senderType,
            Sender = receive.UserId ?? "",
            Target = target,
            Message = content.Count == 1 ? content[0] : new ListMessage([.. content]),
            ReplyId = string.IsNullOrEmpty(receive.MsgId) ? null : receive.MsgId
        };
    }

    /// <summary>
    /// 收到 core 下发的消息，解析成统一消息后注入给所有订阅者，回复再发回 core
    /// </summary>
    internal void OnReceived(GsMessageReceive receive)
    {
        try
        {
            if (ProxyMain.LogMessage)
            {
                GenshinLang.Log.Debug(JsonSerializer.Serialize(receive, GenshinJsonContext.Default.GsMessageReceive));
            }

            var message = BuildMessage(receive);

            if (message is null)
            {
                return;
            }

            _received++;
            _receiveState?.Set(_received);

            // 这条是刚才转出去的消息的回复，直接发回发起的那个实例（QQ / OneBot …）
            var route = FindRoute(receive);

            if (route is not null)
            {
                var origin = new ProxyMessage
                {
                    Id = route.Id,
                    Uuid = route.Uuid,
                    SenderType = route.SenderType,
                    Sender = route.Sender,
                    Target = route.Target,
                    Message = message.Message,
                    ReplyId = route.ReplyId
                };

                _ = Send(origin, message.Message);

                return;
            }

            // 主动推送没有发起者，注入给插件
            var replies = _event?.Dispatch(message);

            if (replies is null)
            {
                return;
            }

            foreach (var reply in replies)
            {
                _ = Send(message, reply);
            }
        }
        catch (Exception e)
        {
            GenshinLang.Log.Error(e, GenshinLang.Get(GenshinLangKey.CoreSendFail));
        }
    }

    /// <summary>
    /// 平台消息转给 gsuid_core
    /// </summary>
    private List<BaseMessage> Forward(ProxyMessage message)
    {
        // 自己的消息在 OnReceived 里处理，这里不再转，避免回环
        if (message.Id == _proxyId)
        {
            return [];
        }

        var source = message.Message;

        // 用户习惯打 /命令，core 的 command_start 是空的时候要自己剥掉，和 NoneBot 的行为对齐
        if (source is TextMessage text && StripCommandStart(text.Text) is { } command)
        {
            source = new TextMessage(command);
        }

        var receive = ToReceive(message, source);

        if (receive is not null)
        {
            RememberRoute(message);
            _ = SendToCoreAsync(receive);
        }

        return [];
    }

    /// <summary>
    /// 剥掉命令前缀，按配置的 CommandStart 来
    /// </summary>
    /// <param name="text">用户输入</param>
    private string StripCommandStart(string text)
    {
        foreach (var start in _data.CommandStart ?? [])
        {
            if (!string.IsNullOrEmpty(start) && text.StartsWith(start, StringComparison.Ordinal))
            {
                return text[start.Length..].TrimStart();
            }
        }

        return text;
    }

    /// <summary>
    /// 记住这条消息是从哪个实例发出去的，core 回复时好送回去
    /// </summary>
    /// <param name="message">发出去的消息</param>
    private void RememberRoute(ProxyMessage message)
    {
        var route = new ProxyMessage
        {
            Id = message.Id,
            Uuid = message.Uuid,
            SenderType = message.SenderType,
            Sender = message.Sender,
            Target = message.Target,
            Message = message.Message,
            ReplyId = message.ReplyId
        };

        if (!string.IsNullOrEmpty(message.ReplyId))
        {
            Trim(_msgRoutes);
            _msgRoutes[message.ReplyId] = route;
        }

        var chatType = message.SenderType == MessageSenderType.Group ? "group" : "direct";
        var chatId = string.IsNullOrEmpty(message.Target) ? message.Sender : message.Target;

        if (!string.IsNullOrEmpty(chatId))
        {
            Trim(_chatRoutes);
            _chatRoutes[chatType + ":" + chatId] = route;
        }
    }

    /// <summary>
    /// 找这条回复是从哪个实例发出去的：先按 msg_id，再按会话兜底
    /// </summary>
    /// <param name="receive">core 的回复</param>
    private ProxyMessage? FindRoute(GsMessageReceive receive)
    {
        if (!string.IsNullOrEmpty(receive.MsgId) && _msgRoutes.TryRemove(receive.MsgId, out var byMsg))
        {
            return byMsg;
        }

        var type = receive.TargetType ?? receive.UserType;
        var id = receive.TargetId ?? receive.GroupId ?? receive.UserId;

        return string.IsNullOrEmpty(id) ? null : _chatRoutes.GetValueOrDefault(type + ":" + id);
    }

    /// <summary>
    /// 路由表满了就清掉一批，别一直涨
    /// </summary>
    private static void Trim(ConcurrentDictionary<string, ProxyMessage> map)
    {
        if (map.Count < RouteLimit)
        {
            return;
        }

        foreach (var key in map.Keys.Take(map.Count - RouteLimit + 1))
        {
            map.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// 发一条回复：先过拦截器，再真正发回 core
    /// </summary>
    /// <param name="context">消息上下文</param>
    /// <param name="reply">要发的消息</param>
    internal Task Send(ProxyMessage context, BaseMessage reply)
    {
        return _send is null ? SendAsync(context, reply) : _send.SendAsync(context, reply);
    }

    /// <inheritdoc/>
    public Task SendAsync(ProxyMessage message, BaseMessage reply)
    {
        var receive = ToReceive(message, reply);

        return receive is null ? Task.CompletedTask : SendToCoreAsync(receive);
    }

    /// <summary>
    /// 统一消息转成 core 的消息体
    /// </summary>
    private GsMessageReceive? ToReceive(ProxyMessage message, BaseMessage reply)
    {
        var content = GenshinMessage.ToGs(reply);

        if (content.Count == 0)
        {
            return null;
        }

        var isGroup = message.SenderType == MessageSenderType.Group;
        var target = string.IsNullOrEmpty(message.Target) ? message.Sender : message.Target;

        return new GsMessageReceive
        {
            BotId = _data.BotId,
            MsgId = message.ReplyId ?? "",
            UserType = isGroup ? "group" : "direct",
            GroupId = isGroup ? target : null,
            UserId = message.Sender,
            Sender = new Dictionary<string, string> { ["user_id"] = message.Sender },
            Content = content
        };
    }

    /// <summary>
    /// 发给 core，失败只记日志
    /// </summary>
    private async Task SendToCoreAsync(GsMessageReceive message)
    {
        if (_client is null || !_client.Connected)
        {
            GenshinLang.Log.Warn(GenshinLang.Get(GenshinLangKey.NotConnected));
            return;
        }

        if (ProxyMain.LogMessage)
        {
            GenshinLang.Log.Debug(JsonSerializer.Serialize(message, GenshinJsonContext.Default.GsMessageReceive));
        }

        try
        {
            await _client.SendAsync(message);
        }
        catch (Exception e)
        {
            GenshinLang.Log.Error(e, GenshinLang.Get(GenshinLangKey.SendFail));
        }
    }

    /// <summary>
    /// 建客户端并挂上回调
    /// </summary>
    private GenshinClient CreateClient()
    {
        var client = new GenshinClient();

        client.MessageReceived += OnReceived;

        client.StateChanged += connected =>
        {
            if (connected)
            {
                GenshinLang.Log.Info(GenshinLang.Get(GenshinLangKey.Connected, _data.Url));
            }
        };

        return client;
    }

    /// <summary>
    /// 连接，失败交给 Tick 重连
    /// </summary>
    private void Connect(Uri url)
    {
        _lastTry = DateTime.Now;

        try
        {
            _client?.StartAsync(url, _data.BotId, _data.Token).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            GenshinLang.Log.Warn(GenshinLang.Get(GenshinLangKey.ConnectFail, url, e.Message));
        }
    }

    /// <summary>
    /// 读取实例配置
    /// </summary>
    private bool ReadConfig()
    {
        try
        {
            if (File.Exists(_configFile))
            {
                var obj = JsonUtils.ToObj(File.ReadAllText(_configFile), SourceGenerationContext.Default.InstanceDataObj);

                if (obj is not null)
                {
                    _data = obj;
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            GenshinLang.Log.Error(e, GenshinLang.Get(GenshinLangKey.ConfigReadError));
            return false;
        }

        return true;
    }

    /// <summary>
    /// 保存实例配置
    /// </summary>
    private void SaveConfig()
    {
        if (string.IsNullOrEmpty(_configFile))
        {
            return;
        }

        try
        {
            File.WriteAllText(_configFile, JsonUtils.ToString(_data, SourceGenerationContext.Default.InstanceDataObj));
        }
        catch (Exception e)
        {
            GenshinLang.Log.Error(e, GenshinLang.Get(GenshinLangKey.ConfigSaveError));
        }
    }
}