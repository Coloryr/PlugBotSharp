using System.Text.Json.Serialization;

namespace PlugBotSharp.OneBotProxy;

/// <summary>
/// 连接模式
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OneBotMode>))]
public enum OneBotMode
{
    /// <summary>
    /// 正向 WebSocket：我们连机器人，收发都在同一条连接上
    /// </summary>
    WsClient,
    /// <summary>
    /// 反向 WebSocket：机器人连我们，收事件走 WS，发消息走 HTTP API（Url）
    /// </summary>
    WsServer,
    /// <summary>
    /// 反向 HTTP 收 + 正向 HTTP 发
    /// </summary>
    Http,
    /// <summary>
    /// 只发消息，走 HTTP API（Url），不监听
    /// </summary>
    Send
}

/// <summary>
/// 实例配置，放在 Instances/{uuid}/config.json
/// </summary>
public record InstanceDataObj
{
    /// <summary>
    /// 连接模式
    /// </summary>
    public OneBotMode Mode { get; set; } = OneBotMode.WsClient;
    /// <summary>
    /// WsClient 模式是 ws 地址，其余模式是 HTTP API 基址
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
    /// 断线后多少秒重连，只对 WsClient 有效
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
