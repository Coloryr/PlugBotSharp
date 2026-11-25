using NLog;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Proxy;

namespace ProxyDemo;

/// <summary>
/// 示例代理组件。
/// 代理负责和外部平台打交道：把收到的消息交给插件，再把插件返回的消息发回去，
/// 平台自己的协议（http 报文、socket、签名之类）都关在代理里面
/// </summary>
public class ProxyMain : IBaseProxy
{
    private const string ConfigFile = "Config.json";

    private Logger _logger;
    private string _path = "";
    private ProxyConfigObj _config = new();

    /// <summary>
    /// 是否把收发的内容写进日志，实例也会读这个开关
    /// </summary>
    internal static bool LogMessage { get; private set; }

    /// <inheritdoc/>
    public bool Init(string path)
    {
        _logger = LogManager.GetLogger("ProxyDemo");
        _logger.Info("示例代理初始化");

        _path = path;
        ReadConfig();

        return true;
    }

    /// <inheritdoc/>
    public void RegisterEventHook(IRegister register)
    {
        // 代理本身不订阅消息，订阅交给实例做
    }

    /// <inheritdoc/>
    public IProxyInstasnce CreateInstance()
    {
        return new DemoInstance(Meta.Info.Id);
    }

    /// <inheritdoc/>
    public void Reload()
    {
        _logger.Info("重载配置");

        ReadConfig();
    }

    /// <inheritdoc/>
    public void Reload(IProxyInstasnce instasnce)
    {
        // 组件把新配置推给实例，实例自己去读 Instances/{uuid}/config.json
    }

    /// <inheritdoc/>
    public Dictionary<string, ConfigMap> ConfigMap()
    {
        return new()
        {
            ["LogMessage"] = new ConfigBoolMap
            {
                Key = "LogMessage",
                Value = _config.LogMessage,
                Default = false,
                Description = "把收到的消息和发出去的回复写进调试日志"
            }
        };
    }

    /// <inheritdoc/>
    public void UpdateMap(Dictionary<string, ConfigMap> data)
    {
        if (data.TryGetValue("LogMessage", out var item) && item is ConfigBoolMap map)
        {
            _config.LogMessage = map.Value;
        }

        LogMessage = _config.LogMessage;
        SaveConfig();
    }

    /// <inheritdoc/>
    public bool Load()
    {
        return true;
    }

    /// <inheritdoc/>
    public bool Unload()
    {
        return true;
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

    /// <summary>
    /// 读组件配置
    /// </summary>
    private void ReadConfig()
    {
        try
        {
            var file = Path.Combine(_path, ConfigFile);

            if (File.Exists(file))
            {
                var obj = JsonUtils.ToObj(File.ReadAllText(file), DemoJsonContext.Default.ProxyConfigObj);

                if (obj is not null)
                {
                    _config = obj;
                }
            }
        }
        catch (Exception e)
        {
            _logger?.Error(e, "组件配置读取失败");
        }

        LogMessage = _config.LogMessage;
    }

    /// <summary>
    /// 写组件配置
    /// </summary>
    private void SaveConfig()
    {
        try
        {
            File.WriteAllText(Path.Combine(_path, ConfigFile),
                JsonUtils.ToString(_config, DemoJsonContext.Default.ProxyConfigObj));
        }
        catch (Exception e)
        {
            _logger?.Error(e, "组件配置保存失败");
        }
    }
}
