using NLog;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core;

namespace PlugBotSharp.QQProxy;

public class ProxyMain : IBaseProxy
{
    /// <summary>
    /// 组件配置文件，放在 Configs/qq 下
    /// </summary>
    private const string ConfigFile = "Config.json";

    private Logger _logger;

    /// <summary>
    /// 组件配置目录
    /// </summary>
    private string _path;

    /// <summary>
    /// 组件配置
    /// </summary>
    private ProxyConfigObj _config = new();

    /// <summary>
    /// 是否记录回调原始报文
    /// </summary>
    public static bool LogPayload { get; private set; }

    /// <summary>
    /// 提前多少秒刷新凭证
    /// </summary>
    public static int TokenRefreshAhead { get; private set; } = 30;

    /// <inheritdoc/>
    public bool Init(string path)
    {
        _logger = LogManager.GetLogger($"QQBot");
        _logger.Info(ProxyLang.GetLang(ProxyLangKey.Init));

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
        return new ProxyInstance(Meta.Info.Id);
    }

    /// <inheritdoc/>
    public void Reload()
    {
        _logger.Info(ProxyLang.GetLang(ProxyLangKey.Reload));

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
            ["LogPayload"] = new ConfigBoolMap
            {
                Key = "LogPayload",
                Value = _config.LogPayload,
                Default = false,
                Description = "打开后会把平台回调的原始报文写进调试日志，排查问题时用"
            },
            ["TokenRefreshAhead"] = new ConfigIntMap
            {
                Key = "TokenRefreshAhead",
                Value = TokenRefreshAhead,
                Min = 5,
                Max = 600,
                Default = 30,
                Description = "access_token 剩余多少秒时提前刷新，所有实例共用",
                Placeholder = "30"
            }
        };
    }

    /// <inheritdoc/>
    public void UpdateMap(Dictionary<string, ConfigMap> data)
    {
        if (data.TryGetValue("LogPayload", out var log) && log is ConfigBoolMap logMap)
        {
            _config.LogPayload = logMap.Value;
        }

        if (data.TryGetValue("TokenRefreshAhead", out var refresh) && refresh is ConfigIntMap refreshMap)
        {
            _config.TokenRefreshAhead = Math.Clamp(refreshMap.Value, 5, 600);
        }

        ApplyConfig();
        SaveConfig();

        _logger?.Info(ProxyLang.GetLang(ProxyLangKey.ConfigSave));
    }

    /// <inheritdoc/>
    public bool Disable()
    {
        return true;
    }

    /// <inheritdoc/>
    public bool Enable()
    {
        return true;
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
                var obj = JsonUtils.ToObj(File.ReadAllText(file), JsonContext.Default.ProxyConfigObj);

                if (obj != null)
                {
                    _config = obj;
                    _logger?.Info(ProxyLang.GetLang(ProxyLangKey.ConfigRead));
                }
            }
        }
        catch (Exception e)
        {
            _logger?.Error(e, ProxyLang.GetLang(ProxyLangKey.ConfigReadError));
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
                JsonUtils.ToString(_config, JsonContext.Default.ProxyConfigObj));
        }
        catch (Exception e)
        {
            _logger?.Error(e, ProxyLang.GetLang(ProxyLangKey.ConfigSaveError));
        }
    }

    /// <summary>
    /// 把配置同步给用到的静态字段
    /// </summary>
    private void ApplyConfig()
    {
        _config.TokenRefreshAhead = Math.Clamp(_config.TokenRefreshAhead, 5, 600);

        LogPayload = _config.LogPayload;
        TokenRefreshAhead = _config.TokenRefreshAhead;
    }
}