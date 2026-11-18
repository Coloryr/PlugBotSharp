namespace PlugBotSharp.Core;

public enum Lang
{
    zh_cn,
    en_us
}

internal enum BotLangKey
{
    Init,
    ErrorConfigSave,
}

internal static class BotLang
{
    public static string GetLang(BotLangKey key, params object[] args)
    {
        switch (BotManager.Lang)
        {
            case Lang.zh_cn:
                switch (key)
                {
                    case BotLangKey.Init:
                        return "核心启动";
                    case BotLangKey.ErrorConfigSave:
                        return "配置文件保存错误";
                }
                break;
            case Lang.en_us:
                switch (key)
                {
                    case BotLangKey.Init:
                        return "";
                }
                break;
        }

        return "";
    }
}