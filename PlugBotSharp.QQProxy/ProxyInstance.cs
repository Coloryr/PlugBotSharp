using NLog;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Proxy;

namespace PlugBotSharp.QQProxy;

public class ProxyInstance : IProxyInstasnce
{
    /// <summary>
    /// 日志
    /// </summary>
    internal static Logger Logger;

    /// <summary>
    /// 配置文件
    /// </summary>
    private string _configFile;
    /// <summary>
    /// 实例配置
    /// </summary>
    private InstanceDataObj _data;

    /// <summary>
    /// 实例ID
    /// </summary>
    private Guid _uuid;

    /// <summary>
    /// 凭证
    /// </summary>
    private string _token;
    /// <summary>
    /// 有效期
    /// </summary>
    private double _expires;
    /// <summary>
    /// 上次更新时间
    /// </summary>
    private DateTime _lastDateTime;

    private Lock _update;
    private bool _isOk;

    /// <inheritdoc/>
    public void Init(string path, Guid uuid)
    {
        _configFile = Path.Combine(path, "config.json");
        _uuid = uuid;
        Logger = LogManager.GetLogger($"QQBot[{uuid.ToString()[0..8]}]");
        Logger.Info(ProxyLang.GetLang(ProxyLangKey.InstanceInit));
    }

    /// <inheritdoc/>
    public async Task<bool> Load()
    {
        Logger.Info(ProxyLang.GetLang(ProxyLangKey.StartLoad));

        try
        {
            if (File.Exists(_configFile))
            {
                var obj = JsonUtils.ToObj(File.ReadAllText(_configFile), JsonContext.Default.InstanceDataObj);
                if (obj != null)
                {
                    Logger.Info(ProxyLang.GetLang(ProxyLangKey.ConfigRead));
                    _data = obj;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, ProxyLang.GetLang(ProxyLangKey.ConfigReadError));
        }

        _data = new InstanceDataObj();

        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> Enable()
    {
        Logger.Info(ProxyLang.GetLang(ProxyLangKey.Enable));

        if (string.IsNullOrWhiteSpace(_data.AppId)
        || string.IsNullOrWhiteSpace(_data.AppSecret))
        {
            Logger.Warn(ProxyLang.GetLang(ProxyLangKey.BotInfoEmpty));

            return false;
        }

        var res = await GetToken();

        if (!res)
        {
            return false;
        }

        _isOk = true;
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> Disable()
    {
        Logger.Info(ProxyLang.GetLang(ProxyLangKey.Disable));

        _isOk = false;
        return true;
    }

    /// <inheritdoc/>
    public void Tick()
    {
        var time = DateTime.Now;

        var pass = time - _lastDateTime;
        _expires -= pass.TotalSeconds;
        if (_expires <= 30)
        {
            Task.Run(async () =>
            {
                _isOk = await GetToken();
            });
        }
    }

    /// <summary>
    /// 获取凭证
    /// </summary>
    /// <returns>是否成功获取</returns>
    private async Task<bool> GetToken()
    {
        _update.Enter();
        var obj = new GetAppAccessTokenObj()
        {
            AppId = _data.AppId,
            Clientecret = _data.AppSecret
        };

        try
        {
            var data = await QQApi.GetToken(obj);
            if (data == null || string.IsNullOrWhiteSpace(data.AccessToken) || data.Code != 0)
            {
                Logger.Error(ProxyLang.GetLang(ProxyLangKey.TokenFail, data?.Code, data?.Message));
                _update.Exit();
                return false;
            }

            _token = data.AccessToken;
            _expires = long.Parse(data.ExpiresIn ?? "0");
            _lastDateTime = DateTime.Now;

            _update.Exit();
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e, ProxyLang.GetLang(ProxyLangKey.TokenError));
        }

        _update.Exit();
        return false;
    }
}
