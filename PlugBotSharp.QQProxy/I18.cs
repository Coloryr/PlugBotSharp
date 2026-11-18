using System;
using PlugBotSharp.Core;

namespace PlugBotSharp.QQProxy;

public enum ProxyLangKey
{
    Init,
    InstanceInit,
}

public static class ProxyLang
{
    public static string GetLang(ProxyLangKey key, params object[] args)
    {
        switch (BotManager.Lang)
        {
            case Lang.zh_cn:
                switch (key)
                {
                    case ProxyLangKey.Init:
                        return "QQ机器人平台初始化";
                    case ProxyLangKey.InstanceInit:
                        return "QQ机器人实例初始化";
                }
                break;
            case Lang.en_us:
                switch (key)
                {
                    case ProxyLangKey.Init:
                        return "";
                }
                break;
        }

        return "";
    }
}
