using NLog;
using PlugBotSharp.Core;

namespace PlugBotSharp.OneBotProxy;

public enum OneBotLangKey
{
    Init,
    Reload,
    Connected,
    ConnectFail,
    Listening,
    ServerStartFail,
    CertNotFound,
    NothingToDo,
    ServerNeedsReceive,
    SendDisabled,
    UrlInvalid,
    TargetInvalid,
    NotConnected,
    SendFail,
    EventError,
    ConfigReadError,
    ConfigSaveError
}

public static class OneBotLang
{
    /// <summary>
    /// 组件日志
    /// </summary>
    public static Logger Log { get; } = LogManager.GetLogger("OneBot");

    /// <summary>
    /// 按当前语言取文案并格式化参数
    /// </summary>
    public static string Get(OneBotLangKey key, params object[] args)
    {
        var text = BotManager.Lang == Lang.en_us ? GetEn(key) : GetZh(key);

        return args.Length == 0 ? text : string.Format(text, args);
    }

    private static string GetZh(OneBotLangKey key)
    {
        return key switch
        {
            OneBotLangKey.Init => "OneBot 代理初始化",
            OneBotLangKey.Reload => "重载配置",
            OneBotLangKey.Connected => "已连接 {0}",
            OneBotLangKey.ConnectFail => "连接 {0} 失败: {1}",
            OneBotLangKey.Listening => "监听 {0}:{1}（{2}）",
            OneBotLangKey.ServerStartFail => "监听 {0}:{1} 失败",
            OneBotLangKey.CertNotFound => "找不到证书 {0}，检查 Certs 目录",
            OneBotLangKey.NothingToDo => "接收和发送都关掉了，实例没有意义",
            OneBotLangKey.ServerNeedsReceive => "服务端模式必须开启接收",
            OneBotLangKey.SendDisabled => "发送已关闭，消息不发出",
            OneBotLangKey.UrlInvalid => "地址无效 {0}",
            OneBotLangKey.TargetInvalid => "回复目标无效 {0}",
            OneBotLangKey.NotConnected => "还没有连上机器人，消息不回复",
            OneBotLangKey.SendFail => "发送消息失败 {0}",
            OneBotLangKey.EventError => "处理上报事件异常",
            OneBotLangKey.ConfigReadError => "配置读取失败",
            OneBotLangKey.ConfigSaveError => "配置保存失败",
            _ => ""
        };
    }

    private static string GetEn(OneBotLangKey key)
    {
        return key switch
        {
            OneBotLangKey.Init => "OneBot proxy init",
            OneBotLangKey.Reload => "Reload config",
            OneBotLangKey.Connected => "Connected to {0}",
            OneBotLangKey.ConnectFail => "Connect {0} failed: {1}",
            OneBotLangKey.Listening => "Listening on {0}:{1} ({2})",
            OneBotLangKey.ServerStartFail => "Listen {0}:{1} failed",
            OneBotLangKey.CertNotFound => "Cert {0} not found, check the Certs folder",
            OneBotLangKey.NothingToDo => "Receive and send are both disabled",
            OneBotLangKey.ServerNeedsReceive => "Server mode requires receive",
            OneBotLangKey.SendDisabled => "Send is disabled, message ignored",
            OneBotLangKey.UrlInvalid => "Url is invalid {0}",
            OneBotLangKey.TargetInvalid => "Reply target is invalid {0}",
            OneBotLangKey.NotConnected => "Bot is not connected, message ignored",
            OneBotLangKey.SendFail => "Send message failed {0}",
            OneBotLangKey.EventError => "Handle event error",
            OneBotLangKey.ConfigReadError => "Config read error",
            OneBotLangKey.ConfigSaveError => "Config save error",
            _ => ""
        };
    }
}