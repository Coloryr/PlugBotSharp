using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PlugBotSharp.QQProxy;

public enum HookType
{
    /// <summary>
    /// Http服务器注册到Core中
    /// </summary>
    WebHook,
    /// <summary>
    /// 使用WebSocket通信
    /// </summary>
    WebSocket,
    /// <summary>
    /// 自己启动Http服务器
    /// </summary>
    WebHost
}

/// <summary>
/// 配置文件
/// </summary>
public record InstanceDataObj
{
    /// <summary>
    /// 机器人ID
    /// </summary>
    public string AppId { get; set; }
    /// <summary>
    /// 机器人密钥
    /// </summary>
    public string AppSecret { get; set; }

    /// <summary>
    /// Http服务器端口
    /// </summary>
    public ushort Port { get; set; }
    /// <summary>
    /// 通信类型
    /// </summary>
    public HookType Type { get; set; }
}

/// <summary>
/// 组件配置，放在 Configs/qq/Config.json
/// </summary>
public record ProxyConfigObj
{
    /// <summary>
    /// 是否记录回调原始报文
    /// </summary>
    public bool LogPayload { get; set; }
    /// <summary>
    /// 提前多少秒刷新凭证
    /// </summary>
    public int TokenRefreshAhead { get; set; } = 30;
}

public record BaseMessageObj
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("op")]
    public int OpCode { get; set; }
    [JsonPropertyName("s")]
    public long Serial { get; set; }
    [JsonPropertyName("t")]
    public string Type { get; set; }
    [JsonPropertyName("d")]
    public JsonObject Data { get; set; }
}

/// <summary>
/// 回调地址验证请求，op 13 的 d 字段
/// </summary>
public record ValidationRequestObj
{
    /// <summary>
    /// 平台下发的明文 token
    /// </summary>
    [JsonPropertyName("plain_token")]
    public string PlainToken { get; set; }
    /// <summary>
    /// 事件时间戳
    /// </summary>
    [JsonPropertyName("event_ts")]
    public string EventTs { get; set; }
}

/// <summary>
/// 回调地址验证响应
/// </summary>
public record ValidationResponseObj
{
    /// <summary>
    /// 原样返回的明文 token
    /// </summary>
    [JsonPropertyName("plain_token")]
    public string PlainToken { get; set; }
    /// <summary>
    /// 签名，hex 小写
    /// </summary>
    [JsonPropertyName("signature")]
    public string Signature { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(InstanceDataObj))]
[JsonSerializable(typeof(ProxyConfigObj))]
[JsonSerializable(typeof(BaseMessageObj))]
[JsonSerializable(typeof(ValidationRequestObj))]
[JsonSerializable(typeof(ValidationResponseObj))]
internal partial class JsonContext : JsonSerializerContext
{

}
