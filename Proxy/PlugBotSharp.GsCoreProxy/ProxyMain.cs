using NLog;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Proxy;

namespace PlugBotSharp.GsCoreProxy;

public class ProxyMain : IBaseProxy
{
    private const string ConfigFile = "Config.json";

    private Logger _logger;
    private string _path = "";
    private ProxyConfigObj _config = new();

    /// <summary>
    /// 是否记录收发报文
    /// </summary>
    public static bool LogMessage { get; private set; }

    /// <inheritdoc/>
    public bool Init(string path)
    {
        _logger = LogManager.GetLogger("GsCore");
        _logger.Info(GenshinLang.Get(GenshinLangKey.Init));

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
        return new GenshinInstance(Meta.Info.Id);
    }

    /// <inheritdoc/>
    public void Reload()
    {
        _logger.Info(GenshinLang.Get(GenshinLangKey.Reload));

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
            ["LogMessage"] = new ConfigBoolMap
            {
                Key = "LogMessage",
                Value = _config.LogMessage,
                Default = false,
                Description = "把发给 gsuid_core 和从 core 收到的报文写进调试日志"
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
            _logger?.Error(e, GenshinLang.Get(GenshinLangKey.ConfigReadError));
        }

        LogMessage = _config.LogMessage;
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
            _logger?.Error(e, GenshinLang.Get(GenshinLangKey.ConfigSaveError));
        }
    }
}