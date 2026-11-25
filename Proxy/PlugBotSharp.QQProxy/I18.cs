using System;
using PlugBotSharp.Core;

namespace PlugBotSharp.QQProxy;

public enum ProxyLangKey
{
    Init,
    InstanceInit,

    StartLoad,
    Reload,
    ConfigRead,
    ConfigReadError,
    ConfigSave,
    ConfigSaveError,
    Enable,
    Disable,
    BotInfoEmpty,

    HttpStart,
    HttpStartError,
    HttpListening,
    HttpStop,
    HttpStopError,
    Callback,
    CallbackValidation,
    CallbackValidationFail,
    CallbackParseError,
    CallbackEvent,
    CallbackResponse,
    CallbackSignatureFail,
    EventParseError,
    ApiError,
    PassiveLimit,
    ActiveFail,
    SendBlocked,
    ActiveQuota,
    SendFail,
    SendError,
    NotReady,
    CertNotFound,

    TokenFail,
    TokenError,
}

public static class ProxyLang
{
    /// <summary>
    /// 按当前语言取文案并格式化参数
    /// </summary>
    public static string GetLang(ProxyLangKey key, params object[] args)
    {
        var text = BotManager.Lang == Lang.en_us ? GetEn(key) : GetZh(key);

        return args.Length == 0 ? text : string.Format(text, args);
    }

    /// <summary>
    /// 中文文案
    /// </summary>
    private static string GetZh(ProxyLangKey key)
    {
        switch (key)
        {
            case ProxyLangKey.Init:
                return "QQ机器人平台初始化";
            case ProxyLangKey.InstanceInit:
                return "QQ机器人实例初始化";

            case ProxyLangKey.StartLoad:
                return "开始加载";
            case ProxyLangKey.Reload:
                return "重载配置";
            case ProxyLangKey.ConfigRead:
                return "配置读取完成";
            case ProxyLangKey.ConfigReadError:
                return "配置读取失败";
            case ProxyLangKey.ConfigSave:
                return "配置已保存";
            case ProxyLangKey.ConfigSaveError:
                return "配置保存失败";
            case ProxyLangKey.Enable:
                return "启用";
            case ProxyLangKey.Disable:
                return "禁用";
            case ProxyLangKey.BotInfoEmpty:
                return "机器人信息为空";

            case ProxyLangKey.HttpStart:
                return "启动 http 服务器";
            case ProxyLangKey.HttpStartError:
                return "http 服务器启动异常";
            case ProxyLangKey.HttpListening:
                return "回调地址 {0}{1}";
            case ProxyLangKey.HttpStop:
                return "停止 http 服务器";
            case ProxyLangKey.HttpStopError:
                return "http 服务器停止异常";
            case ProxyLangKey.Callback:
                return "收到回调 {0}";
            case ProxyLangKey.CallbackValidation:
                return "回调地址验证";
            case ProxyLangKey.CallbackValidationFail:
                return "回调地址验证失败";
            case ProxyLangKey.CallbackParseError:
                return "回调解析失败";
            case ProxyLangKey.CallbackEvent:
                return "收到事件 {0}";
            case ProxyLangKey.CallbackResponse:
                return "回调响应 {0}";
            case ProxyLangKey.CallbackSignatureFail:
                return "回调签名校验失败，事件已丢弃";
            case ProxyLangKey.EventParseError:
                return "事件解析失败 {0} {1}";
            case ProxyLangKey.ApiError:
                return "请求 {0} 失败 {1} {2}";
            case ProxyLangKey.PassiveLimit:
                return "被动回复次数用完 {0}，改成主动发送";
            case ProxyLangKey.ActiveFail:
                return "主动发送失败 {0} {1}，后续消息不再发送";
            case ProxyLangKey.SendBlocked:
                return "消息已停止发送 {0}";
            case ProxyLangKey.ActiveQuota:
                return "群 {0} 今日主动消息已达 {1} 条上限，不再发送";
            case ProxyLangKey.SendFail:
                return "发送消息失败 {0}";
            case ProxyLangKey.SendError:
                return "发送消息异常 {0}";
            case ProxyLangKey.NotReady:
                return "实例未启用，消息不回复";
            case ProxyLangKey.CertNotFound:
                return "找不到证书 {0}，检查 Certs 目录";

            case ProxyLangKey.TokenFail:
                return "获取凭证失败 {0} {1}";
            case ProxyLangKey.TokenError:
                return "获取凭证异常";
        }

        return "";
    }

    /// <summary>
    /// 英文文案
    /// </summary>
    private static string GetEn(ProxyLangKey key)
    {
        switch (key)
        {
            case ProxyLangKey.Init:
                return "QQ bot platform init";
            case ProxyLangKey.InstanceInit:
                return "QQ bot instance init";

            case ProxyLangKey.StartLoad:
                return "Start load";
            case ProxyLangKey.Reload:
                return "Reload config";
            case ProxyLangKey.ConfigRead:
                return "Config loaded";
            case ProxyLangKey.ConfigReadError:
                return "Config read error";
            case ProxyLangKey.ConfigSave:
                return "Config saved";
            case ProxyLangKey.ConfigSaveError:
                return "Config save error";
            case ProxyLangKey.Enable:
                return "Enable";
            case ProxyLangKey.Disable:
                return "Disable";
            case ProxyLangKey.BotInfoEmpty:
                return "Bot info is empty";

            case ProxyLangKey.HttpStart:
                return "Start http server";
            case ProxyLangKey.HttpStartError:
                return "Http server start error";
            case ProxyLangKey.HttpListening:
                return "Callback url {0}{1}";
            case ProxyLangKey.HttpStop:
                return "Stop http server";
            case ProxyLangKey.HttpStopError:
                return "Http server stop error";
            case ProxyLangKey.Callback:
                return "Callback {0}";
            case ProxyLangKey.CallbackValidation:
                return "Callback url validation";
            case ProxyLangKey.CallbackValidationFail:
                return "Callback url validation failed";
            case ProxyLangKey.CallbackParseError:
                return "Callback parse error";
            case ProxyLangKey.CallbackEvent:
                return "Event {0}";
            case ProxyLangKey.CallbackResponse:
                return "Callback response {0}";
            case ProxyLangKey.CallbackSignatureFail:
                return "Callback signature verification failed, event dropped";
            case ProxyLangKey.EventParseError:
                return "Event parse failed {0} {1}";
            case ProxyLangKey.ApiError:
                return "Request {0} failed {1} {2}";
            case ProxyLangKey.PassiveLimit:
                return "Passive reply limit reached {0}, sending actively";
            case ProxyLangKey.ActiveFail:
                return "Active send failed {0} {1}, further messages are dropped";
            case ProxyLangKey.SendBlocked:
                return "Message sending stopped {0}";
            case ProxyLangKey.ActiveQuota:
                return "Group {0} reached the daily active message limit {1}";
            case ProxyLangKey.SendFail:
                return "Send message failed {0}";
            case ProxyLangKey.SendError:
                return "Send message error {0}";
            case ProxyLangKey.NotReady:
                return "Instance is not enabled, messages are ignored";
            case ProxyLangKey.CertNotFound:
                return "Cert {0} not found, check the Certs folder";

            case ProxyLangKey.TokenFail:
                return "Get token failed {0} {1}";
            case ProxyLangKey.TokenError:
                return "Get token error";
        }

        return "";
    }
}