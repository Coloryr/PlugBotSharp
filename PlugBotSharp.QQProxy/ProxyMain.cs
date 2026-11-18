using NLog;
using PlugBotSharp.Core.Proxy;

namespace PlugBotSharp.QQProxy;

public class ProxyMain : IBaseProxy
{
    private Logger _logger;

    public string Id => "qq";

    public string Name => "QQ机器人";

    public string Description => "QQ官方机器人平台";

    public bool Init(string path)
    {
        _logger = LogManager.GetLogger($"QQBot");
        _logger.Info(ProxyLang.GetLang(ProxyLangKey.Init));

        return true;
    }

    public IProxyInstasnce CreateInstance()
    {
        return new ProxyInstance();
    }

    public void ConfigInstance(IProxyInstasnce instasnce)
    {
        if (instasnce is ProxyInstance proxy)
        {

        }
    }

    public bool Disable()
    {
        return true;
    }

    public bool Enable()
    {
        return true;
    }



    public bool Load()
    {
        return true;
    }

    public bool Unload()
    {
        return true;
    }
}
