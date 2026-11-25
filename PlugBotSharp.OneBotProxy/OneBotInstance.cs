using System.Text.Json;
using System.Text.Json.Nodes;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.Core.Proxy;
using PlugBotSharp.OneBotLib;

namespace PlugBotSharp.OneBotProxy;

/// <summary>
/// OneBot 11 实例，按连接模式收事件、发消息
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
    /// 正向 WS 客户端
    /// </summary>
    private OneBotClient? _ws;

    /// <summary>
    /// 正向 HTTP 客户端
    /// </summary>
    private OneBotHttpClient? _http;

    /// <summary>
    /// 反向 WS / 反向 HTTP 服务端
    /// </summary>
    private OneBotServer? _server;

    private bool _enabled;
    private DateTime _lastTry;

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
    public void RegisterEventHook()
    {
        // 实例要订阅其他代理的消息时，在这里注册 EventManager.RegisterMessageHook
    }

    /// <inheritdoc/>
    public bool Enable()
    {
        _enabled = true;

        switch (_data.Mode)
        {
            case OneBotMode.WsServer:
            case OneBotMode.Http:
                SetupHttp();
                return StartServer();

            case OneBotMode.Send:
                SetupHttp();
                return true;

            default:
                if (!Uri.TryCreate(_data.Url, UriKind.Absolute, out var url))
                {
                    OneBotLang.Log.Error(OneBotLang.Get(OneBotLangKey.UrlInvalid, _data.Url));
                    return false;
                }

                _ws ??= CreateClient();
                Connect(url);

                return true;
        }
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
        if (!_enabled || _data.Mode != OneBotMode.WsClient || _ws is null || _ws.Connected)
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
            ["Mode"] = new ConfigEnumMap
            {
                Key = "Mode",
                Value = _data.Mode.ToString(),
                Default = nameof(OneBotMode.WsClient),
                Options = [.. Enum.GetNames<OneBotMode>()]
            },
            ["Url"] = new ConfigStringMap
            {
                Key = "Url",
                Value = _data.Url,
                Default = "ws://127.0.0.1:3001",
                MaxLength = 200
            },
            ["Host"] = new ConfigStringMap
            {
                Key = "Host",
                Value = _data.Host,
                Default = "127.0.0.1",
                MaxLength = 64
            },
            ["Port"] = new ConfigIntMap
            {
                Key = "Port",
                Value = _data.Port,
                Min = 1,
                Max = 65535,
                Default = 3002
            },
            ["Token"] = new ConfigStringMap
            {
                Key = "Token",
                Value = _data.Token,
                Secret = true
            },
            ["ReconnectSeconds"] = new ConfigIntMap
            {
                Key = "ReconnectSeconds",
                Value = _data.ReconnectSeconds,
                Min = 3,
                Max = 600,
                Default = 5
            }
        };
    }

    /// <inheritdoc/>
    public void UpdateMap(Dictionary<string, ConfigMap> data)
    {
        if (data.TryGetValue("Mode", out var mode) && mode is ConfigEnumMap modeMap
            && Enum.TryParse<OneBotMode>(modeMap.Value, true, out var parsed))
        {
            _data.Mode = parsed;
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
        var segments = OneBotMessage.WithReply(OneBotMessage.ToSegments(reply), message.ReplyId);
        var isGroup = message.SenderType == MessageSenderType.Group;
        var target = isGroup ? message.Target : message.Sender;

        if (!long.TryParse(target, out var id))
        {
            OneBotLang.Log.Warn(OneBotLang.Get(OneBotLangKey.TargetInvalid, target));
            return;
        }

        var param = new JsonObject
        {
            [isGroup ? "group_id" : "user_id"] = id,
            ["message"] = segments,
            ["auto_escape"] = false
        };

        var action = isGroup ? "send_group_msg" : "send_private_msg";

        // 正向 WS 有连接就直接走 socket，其余模式走 HTTP API
        var res = _ws is { Connected: true }
            ? await _ws.CallAsync(action, param)
            : await (_http?.CallAsync(action, param) ?? Task.FromResult<JsonElement?>(null));

        if (res is null)
        {
            OneBotLang.Log.Warn(OneBotLang.Get(OneBotLangKey.SendFail, target));
        }
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

        try
        {
            _server.StartAsync(_data.Host, _data.Port, _data.Token).GetAwaiter().GetResult();

            OneBotLang.Log.Info(OneBotLang.Get(OneBotLangKey.Listening, _data.Host, _data.Port, _data.Mode));

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
            _server?.StopAsync().GetAwaiter().GetResult();
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
        if (ProxyMain.LogEvent)
        {
            OneBotLang.Log.Debug(evt.ToString());
        }

        var message = OneBotEvent.Parse(_proxyId, _uuid, evt);

        if (message is null)
        {
            return;
        }

        var replies = EventManager.HandleMessage(message);

        foreach (var reply in replies)
        {
            _ = SendAsync(message, reply);
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
