using System.Text.Json;
using System.Text.Json.Nodes;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.OneBotLib;

namespace PlugBotSharp.OneBotProxy;

/// <summary>
/// OneBot 实例，按传输方式、客户端 / 服务端、收发开关工作
/// </summary>
public class OneBotInstance : IProxyInstasnce
{
    private readonly string _proxyId;

    /// <summary>
    /// 配置文件
    /// </summary>
    private string _configFile = "";

    /// <summary>
    /// 实例配置
    /// </summary>
    private InstanceDataObj _data = new();

    private Guid _uuid;

    /// <summary>
    /// 客户端连接（WebSocket + 客户端）
    /// </summary>
    private OneBotClient? _ws;

    /// <summary>
    /// 正向 HTTP 客户端
    /// </summary>
    private OneBotHttpClient? _http;

    /// <summary>
    /// 服务端（WebSocket 或 HTTP 都能接）
    /// </summary>
    private OneBotServer? _server;

    private bool _enabled;
    private DateTime _lastTry;

    /// <summary>
    /// 分发句柄，注册阶段拿到
    /// </summary>
    private ProxyEventHandle? _dispatch;

    /// <summary>
    /// 发送句柄，回复走它，先过插件的拦截器再真正发出去
    /// </summary>
    private SendHandle? _send;

    /// <summary>
    /// 创建实例
    /// </summary>
    /// <param name="proxyId">代理组件 id</param>
    public OneBotInstance(string proxyId)
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
        // 收到消息后交给插件
        _dispatch = register.RegisterProxyEvent();

        // 回复先走发送句柄，拦截器放行才真正发
        _send = register.RegisterSend();

    }

    /// <summary>
    /// 把收到的消息交给插件，返回要回复的消息
    /// </summary>
    /// <param name="message">收到的消息</param>
    internal List<BaseMessage> Dispatch(ProxyMessage message)
    {
        return (_dispatch ??= new ProxyEventHandle()).Dispatch(message);
    }

    /// <summary>
    /// 发一条回复：先过拦截器，再真正发到平台
    /// </summary>
    /// <param name="context">消息上下文</param>
    /// <param name="reply">要发的消息</param>
    internal Task Send(ProxyMessage context, BaseMessage reply)
    {
        return _send is null ? SendAsync(context, reply) : _send.SendAsync(context, reply);
    }

    /// <inheritdoc/>
    public bool Enable()
    {
        if (!_data.Receive && !_data.Send)
        {
            OneBotLang.Log.Warn(OneBotLang.Get(OneBotLangKey.NothingToDo));
            return false;
        }

        if (!_data.Receive && _data.Role == OneBotRole.Server)
        {
            OneBotLang.Log.Warn(OneBotLang.Get(OneBotLangKey.ServerNeedsReceive));
            return false;
        }

        _enabled = true;

        // 服务端：监听，收事件
        if (_data.Role == OneBotRole.Server)
        {
            if (!StartServer())
            {
                return false;
            }

            // 服务端模式发消息走 HTTP API
            if (_data.Send)
            {
                SetupHttp();
            }

            return true;
        }

        // 客户端 + WebSocket：连上去，收发都在这条连接上
        if (_data.Transport == OneBotTransport.WebSocket)
        {
            if (!Uri.TryCreate(_data.Url, UriKind.Absolute, out var url))
            {
                OneBotLang.Log.Error(OneBotLang.Get(OneBotLangKey.UrlInvalid, _data.Url));
                return false;
            }

            _ws ??= CreateClient();
            Connect(url);

            return true;
        }

        // 客户端 + HTTP：只发
        SetupHttp();

        return true;
    }

    /// <inheritdoc/>
    public bool Disable()
    {
        _enabled = false;

        StopAll();

        return true;
    }

    /// <inheritdoc/>
    public bool Unload()
    {
        _enabled = false;

        StopAll();

        _ws?.Dispose();
        _ws = null;

        _http?.Dispose();
        _http = null;

        return true;
    }

    /// <inheritdoc/>
    public void Tick()
    {
        if (!_enabled
            || _data.Role != OneBotRole.Client
            || _data.Transport != OneBotTransport.WebSocket
            || _ws is null
            || _ws.Connected)
        {
            return;
        }

        var interval = Math.Max(3, _data.ReconnectSeconds);

        if ((DateTime.Now - _lastTry).TotalSeconds < interval)
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
            ["Protocol"] = new ConfigEnumMap
            {
                Key = "Protocol",
                Value = _data.Protocol.ToString(),
                Default = nameof(OneBotVersion.V11),
                Options = [.. Enum.GetNames<OneBotVersion>()],
                Description = "机器人实现的协议版本，v11 用 post_type 上报，v12 用 type"
            },
            ["Transport"] = new ConfigEnumMap
            {
                Key = "Transport",
                Value = _data.Transport.ToString(),
                Default = nameof(OneBotTransport.WebSocket),
                Options = [.. Enum.GetNames<OneBotTransport>()],
                Description = "WebSocket 收事件走长连接；Http 靠机器人 POST 到我们"
            },
            ["Role"] = new ConfigEnumMap
            {
                Key = "Role",
                Value = _data.Role.ToString(),
                Default = nameof(OneBotRole.Client),
                Options = [.. Enum.GetNames<OneBotRole>()],
                Description = "Client 是我们连机器人，Server 是机器人连我们（要填 Host / Port）"
            },
            ["Receive"] = new ConfigBoolMap
            {
                Key = "Receive",
                Value = _data.Receive,
                Default = true,
                Description = "关掉就只发不收，服务端模式必须开着"
            },
            ["Send"] = new ConfigBoolMap
            {
                Key = "Send",
                Value = _data.Send,
                Default = true,
                Description = "关掉就只收不发，插件返回的消息会被丢弃"
            },
            ["Url"] = new ConfigStringMap
            {
                Key = "Url",
                Value = _data.Url,
                Default = "ws://127.0.0.1:3001",
                MaxLength = 200,
                Description = "客户端模式是机器人的 ws 地址；服务端模式是发消息用的 HTTP API 基址",
                Placeholder = "ws://127.0.0.1:3001"
            },
            ["Host"] = new ConfigStringMap
            {
                Key = "Host",
                Value = _data.Host,
                Default = "127.0.0.1",
                MaxLength = 64,
                Description = "服务端模式监听地址，只给本机用就填 127.0.0.1",
                Placeholder = "127.0.0.1"
            },
            ["Port"] = new ConfigIntMap
            {
                Key = "Port",
                Value = _data.Port,
                Min = 1,
                Max = 65535,
                Default = 3002,
                Description = "服务端模式监听端口，WebSocket 在 /ws，HTTP 在 /",
                Placeholder = "3002"
            },
            ["Token"] = new ConfigStringMap
            {
                Key = "Token",
                Value = _data.Token,
                Secret = true,
                Description = "机器人的 access_token，服务端模式会校验 Authorization: Bearer",
                Placeholder = "access_token，没设就留空"
            },
            ["Certificate"] = new ConfigEnumMap
            {
                Key = "Certificate",
                Value = _data.Certificate,
                Default = "",
                Options = CertOptions(_data.Certificate),
                Description = "从 Certs 目录里选证书，服务端模式填了就走 https / wss；空表示不用"
            },
            ["CertificatePassword"] = new ConfigStringMap
            {
                Key = "CertificatePassword",
                Value = _data.CertificatePassword,
                Secret = true,
                Description = "pfx 的密码，导出证书时设置的",
                Placeholder = "证书密码"
            },
            ["ReconnectSeconds"] = new ConfigIntMap
            {
                Key = "ReconnectSeconds",
                Value = _data.ReconnectSeconds,
                Min = 3,
                Max = 600,
                Default = 5,
                Description = "客户端 WebSocket 断线后多少秒重连",
                Placeholder = "5"
            }
        };
    }

    /// <inheritdoc/>
    public void UpdateMap(Dictionary<string, ConfigMap> data)
    {
        if (data.TryGetValue("Protocol", out var protocol) && protocol is ConfigEnumMap protocolMap
            && Enum.TryParse<OneBotVersion>(protocolMap.Value, true, out var version))
        {
            _data.Protocol = version;
        }

        if (data.TryGetValue("Transport", out var transport) && transport is ConfigEnumMap transportMap
            && Enum.TryParse<OneBotTransport>(transportMap.Value, true, out var transportValue))
        {
            _data.Transport = transportValue;
        }

        if (data.TryGetValue("Role", out var role) && role is ConfigEnumMap roleMap
            && Enum.TryParse<OneBotRole>(roleMap.Value, true, out var roleValue))
        {
            _data.Role = roleValue;
        }

        if (data.TryGetValue("Receive", out var receive) && receive is ConfigBoolMap receiveMap)
        {
            _data.Receive = receiveMap.Value;
        }

        if (data.TryGetValue("Send", out var send) && send is ConfigBoolMap sendMap)
        {
            _data.Send = sendMap.Value;
        }

        if (data.TryGetValue("Url", out var url) && url is ConfigStringMap urlMap)
        {
            _data.Url = urlMap.Value ?? "";
        }

        if (data.TryGetValue("Host", out var host) && host is ConfigStringMap hostMap)
        {
            _data.Host = hostMap.Value ?? "";
        }

        if (data.TryGetValue("Port", out var port) && port is ConfigIntMap portMap)
        {
            _data.Port = Math.Clamp(portMap.Value, 1, 65535);
        }

        if (data.TryGetValue("Token", out var token) && token is ConfigStringMap tokenMap)
        {
            _data.Token = tokenMap.Value ?? "";
        }

        if (data.TryGetValue("Certificate", out var certificate) && certificate is ConfigEnumMap certificateMap)
        {
            _data.Certificate = certificateMap.Value ?? "";
        }

        if (data.TryGetValue("CertificatePassword", out var certificatePassword) && certificatePassword is ConfigStringMap certificatePasswordMap)
        {
            _data.CertificatePassword = certificatePasswordMap.Value ?? "";
        }

        if (data.TryGetValue("ReconnectSeconds", out var reconnect) && reconnect is ConfigIntMap reconnectMap)
        {
            _data.ReconnectSeconds = Math.Clamp(reconnectMap.Value, 3, 600);
        }

        SaveConfig();
    }

    /// <summary>
    /// 回复一条消息
    /// </summary>
    /// <param name="message">收到的消息</param>
    /// <param name="reply">要回复的消息</param>
    public async Task SendAsync(ProxyMessage message, BaseMessage reply)
    {
        if (!_data.Send)
        {
            OneBotLang.Log.Warn(OneBotLang.Get(OneBotLangKey.SendDisabled));
            return;
        }

        var segments = OneBotMessage.WithReply(OneBotMessage.ToSegments(reply, _data.Protocol), message.ReplyId);
        var isGroup = message.SenderType == MessageSenderType.Group;
        var target = isGroup ? message.Target : message.Sender;

        if (string.IsNullOrEmpty(target))
        {
            OneBotLang.Log.Warn(OneBotLang.Get(OneBotLangKey.TargetInvalid, target));
            return;
        }

        var (action, param) = OneBotAction.SendMessage(_data.Protocol, isGroup, target, segments);

        // WebSocket 客户端有连接就直接走 socket，其余模式走 HTTP API
        var res = _ws is { Connected: true }
            ? await _ws.CallAsync(action, param)
            : await (_http?.CallAsync(action, param) ?? Task.FromResult<JsonElement?>(null));

        if (res is null)
        {
            OneBotLang.Log.Warn(OneBotLang.Get(OneBotLangKey.SendFail, target));
        }
    }

    /// <summary>
    /// 证书下拉的选项：空 + Certs 目录里的证书，当前值不在里面时补上
    /// </summary>
    private static List<string> CertOptions(string current)
    {
        var options = new List<string> { "" };
        options.AddRange(CertStore.Names());

        if (!string.IsNullOrEmpty(current) && !options.Contains(current, StringComparer.OrdinalIgnoreCase))
        {
            options.Add(current);
        }

        return options;
    }

    /// <summary>
    /// 建 WS 客户端并挂上事件
    /// </summary>
    private OneBotClient CreateClient()
    {
        var client = new OneBotClient();

        client.EventReceived += HandleEvent;

        client.StateChanged += connected =>
        {
            if (connected)
            {
                OneBotLang.Log.Info(OneBotLang.Get(OneBotLangKey.Connected, _data.Url));
            }
        };

        return client;
    }

    /// <summary>
    /// 准备 HTTP 客户端
    /// </summary>
    private void SetupHttp()
    {
        _http ??= new OneBotHttpClient();
        _http.Setup(_data.Url, _data.Token);
    }

    /// <summary>
    /// 启动服务端，失败返回 false
    /// </summary>
    private bool StartServer()
    {
        _server ??= CreateServer();

        var webSocket = _data.Transport == OneBotTransport.WebSocket;

        try
        {
            if (!_server.Start(this, _data.Host, _data.Port, _data.Token, webSocket, !webSocket,
                _data.Certificate, _data.CertificatePassword))
            {
                return false;
            }

            OneBotLang.Log.Info(OneBotLang.Get(OneBotLangKey.Listening, _data.Host, _data.Port, _data.Transport));

            return true;
        }
        catch (Exception e)
        {
            OneBotLang.Log.Error(e, OneBotLang.Get(OneBotLangKey.ServerStartFail, _data.Host, _data.Port));
            return false;
        }
    }

    /// <summary>
    /// 建服务端并挂上事件
    /// </summary>
    private OneBotServer CreateServer()
    {
        var server = new OneBotServer();

        server.EventReceived += HandleEvent;

        return server;
    }

    /// <summary>
    /// 连接，失败交给 Tick 重连
    /// </summary>
    private void Connect(Uri url)
    {
        _lastTry = DateTime.Now;

        try
        {
            _ws?.StartAsync(url, _data.Token).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            OneBotLang.Log.Warn(OneBotLang.Get(OneBotLangKey.ConnectFail, url, e.Message));
        }
    }

    /// <summary>
    /// 停掉所有连接
    /// </summary>
    private void StopAll()
    {
        try
        {
            _ws?.StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // 断开失败不影响停用
        }

        try
        {
            _server?.Stop();
        }
        catch
        {
            // 停止失败不影响停用
        }
    }

    /// <summary>
    /// 处理上报事件
    /// </summary>
    private void HandleEvent(JsonElement evt)
    {
        if (!_data.Receive)
        {
            return;
        }

        if (ProxyMain.LogEvent)
        {
            OneBotLang.Log.Debug(evt.ToString());
        }

        var message = OneBotEvent.Parse(_proxyId, _uuid, evt, _data.Protocol);

        if (message is null)
        {
            return;
        }

        var replies = Dispatch(message);

        foreach (var reply in replies)
        {
            _ = Send(message, reply);
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
            OneBotLang.Log.Error(e, OneBotLang.Get(OneBotLangKey.ConfigReadError));
            return false;
        }

        // 没有配置文件时用默认值，面板里先建实例再填 Url
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
            OneBotLang.Log.Error(e, OneBotLang.Get(OneBotLangKey.ConfigSaveError));
        }
    }
}
