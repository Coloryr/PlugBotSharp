using NLog;
using PlugBotSharp.Core.Proxy;

namespace PlugBotSharp.QQProxy;

public class ProxyMain : IBaseProxy
{
    private Logger _logger;

    /// <inheritdoc/>
    public bool Init(string path)
    {
        _logger = LogManager.GetLogger($"QQBot");
        _logger.Info(ProxyLang.GetLang(ProxyLangKey.Init));

        return true;
    }

    /// <inheritdoc/>
    public IProxyInstasnce CreateInstance()
    {
        return new ProxyInstance();
    }

    /// <inheritdoc/>
    public void ConfigInstance(IProxyInstasnce instasnce)
    {
        if (instasnce is ProxyInstance proxy)
        {

        }
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
}
