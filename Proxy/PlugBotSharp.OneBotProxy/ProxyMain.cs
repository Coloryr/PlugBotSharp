using NLog;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core;

namespace PlugBotSharp.OneBotProxy;

public class ProxyMain : IBaseProxy
{
    private const string ConfigFile = "Config.json";

    private Logger _logger;

    private string _path;

    private ProxyConfigObj _config = new();

    /// <summary>
    /// 是否记录上报的原始事件
    /// </summary>
    public static bool LogEvent { get; private set; }

    /// <inheritdoc/>
    public bool Init(string path)
    {
        _logger = LogManager.GetLogger("OneBot");
        _logger.Info(OneBotLang.Get(OneBotLangKey.Init));

        _path = path;
        LoadConfig();

        return true;
    }

    /// <inheritdoc/>
    public void RegisterEventHook(PlugBotSharp.Core.IRegister register)
    {
    }

    /// <inheritdoc/>
    public IProxyInstasnce CreateInstance()
    {
        return new OneBotInstance(Meta.Info.Id);
    }

    /// <inheritdoc/>
    public void Reload()
    {
        _logger.Info(OneBotLang.Get(OneBotLangKey.Reload));

        LoadConfig();
    }

    /// <inheritdoc/>
    public void Reload(IProxyInstasnce instasnce)
    {
    }

    /// <inheritdoc/>
    public Dictionary<string, ConfigMap> ConfigMap()
    {
        return new()
        {
            ["LogEvent"] = new ConfigBoolMap
            {
                Key = "LogEvent",
                Value = _config.LogEvent,
                Default = false,
                Description = "打开后会把机器人上报的原始事件写进调试日志"
            }
        };
    }

    /// <inheritdoc/>
    public void UpdateMap(Dictionary<string, ConfigMap> data)
    {
        if (data.TryGetValue("LogEvent", out var item) && item is ConfigBoolMap map)
        {
            _config.LogEvent = map.Value;
        }

        ApplyConfig();
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
    /// 读取组件配置
    /// </summary>
    private void LoadConfig()
    {
        try
        {
            var file = Path.Combine(_path, ConfigFile);

            if (File.Exists(file))
            {
                var obj = JsonUtils.ToObj(File.ReadAllText(file), SourceGenerationContext.Default.ProxyConfigObj);

                if (obj is not null)
                {
                    _config = obj;
                }
            }
        }
        catch (Exception e)
        {
            _logger?.Error(e, OneBotLang.Get(OneBotLangKey.ConfigReadError));
        }

        ApplyConfig();
    }

    /// <summary>
    /// 保存组件配置
    /// </summary>
    private void SaveConfig()
    {
        try
        {
            File.WriteAllText(Path.Combine(_path, ConfigFile),
                JsonUtils.ToString(_config, SourceGenerationContext.Default.ProxyConfigObj));
        }
        catch (Exception e)
        {
            _logger?.Error(e, OneBotLang.Get(OneBotLangKey.ConfigSaveError));
        }
    }

    /// <summary>
    /// 把配置同步给静态字段
    /// </summary>
    private void ApplyConfig()
    {
        LogEvent = _config.LogEvent;
    }
}