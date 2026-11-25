using System.Text.Json;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.Core.Proxy;

namespace PlugBotSharp.GenshinProxy;

/// <summary>
/// gsuid_core 实例：把平台消息转给 core，再把 core 的消息交给对应平台实例发送
/// </summary>
public class GenshinInstance : IProxyInstasnce
{
    private readonly string _proxyId;

    private string _configFile = "";
    private InstanceDataObj _data = new();
    private Guid _uuid;

    private GenshinClient? _client;

    private bool _enabled;
    private DateTime _lastTry;

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
    public void RegisterEventHook()
    {
        // 订阅所有平台的消息，转给 gsuid_core
        ComponentApi.RegisterMessageHook(this, Forward);
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

        SaveConfig();
    }

    /// <summary>
    /// 平台消息转给 gsuid_core
    /// </summary>
    private List<BaseMessage> Forward(ProxyMessage message)
    {
        // 别的组件发出去的消息不再转，避免回环
        if (message.Id == _proxyId)
        {
            return [];
        }

        var content = GenshinMessage.ToGs(message.Message);

        if (content.Count == 0)
        {
            return [];
        }

        var isGroup = message.SenderType == MessageSenderType.Group;
        var target = string.IsNullOrEmpty(message.Target) ? message.Sender : message.Target;

        var receive = new GsMessageReceive
        {
            BotId = _data.BotId,
            MsgId = message.ReplyId ?? "",
            UserType = isGroup ? "group" : "direct",
            GroupId = isGroup ? target : null,
            UserId = message.Sender,
            Sender = new Dictionary<string, string> { ["user_id"] = message.Sender },
            Content = content
        };

        _ = SendToCoreAsync(receive);

        return [];
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