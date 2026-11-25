using System.Text.Json.Serialization;

namespace PlugBotSharp.GenshinProxy;

/// <summary>
/// 实例配置，放在 Instances/{uuid}/config.json
/// </summary>
public record InstanceDataObj
{
    /// <summary>
    /// gsuid_core 地址，例如 ws://127.0.0.1:8765
    /// </summary>
    public string Url { get; set; } = "ws://127.0.0.1:8765";
    /// <summary>
    /// 连接用的 bot_id，core 用它区分不同的适配器
    /// </summary>
    public string BotId { get; set; } = "PlugBotSharp";
    /// <summary>
    /// 连接密钥，对应 core 的 ws token
    /// </summary>
    public string Token { get; set; } = "";
    /// <summary>
    /// 断线后多少秒重连
    /// </summary>
    public int ReconnectSeconds { get; set; } = 5;
}

/// <summary>
/// 组件配置，放在 Configs/genshin/Config.json
/// </summary>
public record ProxyConfigObj
{
    /// <summary>
    /// 是否记录收到和发出的报文
    /// </summary>
    public bool LogMessage { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(InstanceDataObj))]
[JsonSerializable(typeof(ProxyConfigObj))]
internal partial class SourceGenerationContext : JsonSerializerContext
{

}
