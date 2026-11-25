using NLog;
using PlugBotSharp.Core;

namespace PlugBotSharp.GsCoreProxy;

public enum GenshinLangKey
{
    Init,
    Reload,
    Connected,
    ConnectFail,
    UrlInvalid,
    NotConnected,
    SendFail,
    RouteMissing,
    CoreSendFail,
    ConfigReadError,
    ConfigSaveError
}

public static class GenshinLang
{
    /// <summary>
    /// 组件日志
    /// </summary>
    public static Logger Log { get; } = LogManager.GetLogger("GsCore");

    /// <summary>
    /// 按当前语言取文案并格式化参数
    /// </summary>
    public static string Get(GenshinLangKey key, params object[] args)
    {
        var text = BotManager.Lang == Lang.en_us ? GetEn(key) : GetZh(key);

        return args.Length == 0 ? text : string.Format(text, args);
    }

    private static string GetZh(GenshinLangKey key)
    {
        return key switch
        {
            GenshinLangKey.Init => "gsuid_core 代理初始化",
            GenshinLangKey.Reload => "重载配置",
            GenshinLangKey.Connected => "已连接 gsuid_core {0}",
            GenshinLangKey.ConnectFail => "连接 {0} 失败: {1}",
            GenshinLangKey.UrlInvalid => "地址无效 {0}",
            GenshinLangKey.NotConnected => "还没有连上 gsuid_core，消息不转发",
            GenshinLangKey.SendFail => "发给 gsuid_core 失败",
            GenshinLangKey.RouteMissing => "找不到 {0} 对应的平台实例",
            GenshinLangKey.CoreSendFail => "处理 gsuid_core 下发消息异常",
            GenshinLangKey.ConfigReadError => "配置读取失败",
            GenshinLangKey.ConfigSaveError => "配置保存失败",
            _ => ""
        };
    }

    private static string GetEn(GenshinLangKey key)
    {
        return key switch
        {
            GenshinLangKey.Init => "Genshin proxy init",
            GenshinLangKey.Reload => "Reload config",
            GenshinLangKey.Connected => "Connected to gsuid_core {0}",
            GenshinLangKey.ConnectFail => "Connect {0} failed: {1}",
            GenshinLangKey.UrlInvalid => "Url is invalid {0}",
            GenshinLangKey.NotConnected => "gsuid_core is not connected, message ignored",
            GenshinLangKey.SendFail => "Send to gsuid_core failed",
            GenshinLangKey.RouteMissing => "No platform instance for {0}",
            GenshinLangKey.CoreSendFail => "Handle gsuid_core message error",
            GenshinLangKey.ConfigReadError => "Config read error",
            GenshinLangKey.ConfigSaveError => "Config save error",
            _ => ""
        };
    }
}