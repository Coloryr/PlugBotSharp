using System.Text.Json.Serialization;
using PlugBotSharp.OneBotLib;

namespace PlugBotSharp.OneBotProxy;

/// <summary>
/// 传输方式
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OneBotTransport>))]
public enum OneBotTransport
{
    /// <summary>
    /// WebSocket
    /// </summary>
    WebSocket,
    /// <summary>
    /// HTTP
    /// </summary>
    Http
}

/// <summary>
/// 我们站在哪一边
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OneBotRole>))]
public enum OneBotRole
{
    /// <summary>
    /// 客户端，我们主动连机器人
    /// </summary>
    Client,
    /// <summary>
    /// 服务端，机器人连我们
    /// </summary>
    Server
}

/// <summary>
/// 实例配置，放在 Instances/{uuid}/config.json
/// </summary>
public record InstanceDataObj
{
    /// <summary>
    /// 协议版本
    /// </summary>
    public OneBotVersion Protocol { get; set; } = OneBotVersion.V11;
    /// <summary>
    /// 传输方式
    /// </summary>
    public OneBotTransport Transport { get; set; } = OneBotTransport.WebSocket;
    /// <summary>
    /// 客户端还是服务端
    /// </summary>
    public OneBotRole Role { get; set; } = OneBotRole.Client;
    /// <summary>
    /// 是否接收事件
    /// </summary>
    public bool Receive { get; set; } = true;
    /// <summary>
    /// 是否允许发消息
    /// </summary>
    public bool Send { get; set; } = true;
    /// <summary>
    /// 客户端模式连的目标地址（ws:// 或 http://），也是服务端模式发消息用的 HTTP API 基址
    /// </summary>
    public string Url { get; set; } = "ws://127.0.0.1:3001";
    /// <summary>
    /// 服务端模式监听地址
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";
    /// <summary>
    /// 服务端模式监听端口
    /// </summary>
    public int Port { get; set; } = 3002;
    /// <summary>
    /// 访问密钥，对应 OneBot 实现的 access_token
    /// </summary>
    public string Token { get; set; } = "";
    /// <summary>
    /// IIS 导出的 pfx 证书路径，服务端模式填了就用 https / wss
    /// </summary>
    public string Certificate { get; set; } = "";
    /// <summary>
    /// 证书密码
    /// </summary>
    public string CertificatePassword { get; set; } = "";
    /// <summary>
    /// 客户端断线后多少秒重连
    /// </summary>
    public int ReconnectSeconds { get; set; } = 5;
}

/// <summary>
/// 组件配置，放在 Configs/onebot/Config.json
/// </summary>
public record ProxyConfigObj
{
    /// <summary>
    /// 是否记录上报的原始事件
    /// </summary>
    public bool LogEvent { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(InstanceDataObj))]
[JsonSerializable(typeof(ProxyConfigObj))]
internal partial class SourceGenerationContext : JsonSerializerContext
{

}