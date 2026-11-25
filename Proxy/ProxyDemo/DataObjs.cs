using System.Text.Json.Serialization;

namespace ProxyDemo;

/// <summary>
/// 组件配置 Configs/proxydemo/Config.json
/// </summary>
internal class ProxyConfigObj
{
    /// <summary>
    /// 把收发的内容写进日志
    /// </summary>
    public bool LogMessage { get; set; }
}

/// <summary>
/// 实例配置 Instances/{uuid}/config.json
/// </summary>
internal class InstanceDataObj
{
    /// <summary>
    /// 默认的回调地址，插件返回的消息会 POST 到这里；为空就把回复直接放回 http 响应
    /// </summary>
    public string Callback { get; set; } = "";

    /// <summary>
    /// 回调超时秒数
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// 外部程序推进来的消息
/// </summary>
internal class DemoMessageObj
{
    /// <summary>
    /// 会话类型：group / user，默认 user
    /// </summary>
    public string SenderType { get; set; } = "user";

    /// <summary>
    /// 发送者
    /// </summary>
    public string Sender { get; set; } = "";

    /// <summary>
    /// 回复目标，群号之类，为空时用发送者
    /// </summary>
    public string Target { get; set; } = "";

    /// <summary>
    /// 平台消息 id，用于引用回复
    /// </summary>
    public string ReplyId { get; set; } = "";

    /// <summary>
    /// 文本内容
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// 这一条的回调地址，填了优先于实例配置
    /// </summary>
    public string Callback { get; set; } = "";
}

/// <summary>
/// 发回外部程序的消息
/// </summary>
internal class DemoReplyObj
{
    /// <summary>
    /// 谁发的
    /// </summary>
    public string Sender { get; set; } = "";

    /// <summary>
    /// 回到哪个会话
    /// </summary>
    public string Target { get; set; } = "";

    /// <summary>
    /// 文本内容
    /// </summary>
    public string Text { get; set; } = "";
}

/// <summary>
/// http 接口的响应
/// </summary>
internal class DemoResultObj
{
    /// <summary>
    /// 成功与否
    /// </summary>
    public bool Ok { get; set; }

    /// <summary>
    /// 失败原因
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// 插件返回的消息，配了回调地址时这里是空的
    /// </summary>
    public List<DemoReplyObj> Replies { get; set; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ProxyConfigObj))]
[JsonSerializable(typeof(InstanceDataObj))]
[JsonSerializable(typeof(DemoMessageObj))]
[JsonSerializable(typeof(DemoReplyObj))]
[JsonSerializable(typeof(DemoResultObj))]
internal partial class DemoJsonContext : JsonSerializerContext
{
}
