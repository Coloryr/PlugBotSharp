using System.Text.Json.Serialization;

namespace PlugBotSharp.OneBotLib;

/// <summary>
/// OneBot 版本
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OneBotVersion>))]
public enum OneBotVersion
{
    /// <summary>
    /// OneBot 11，CQ 码那套，事件用 post_type / message_type
    /// </summary>
    V11,
    /// <summary>
    /// OneBot 12，纯 JSON 段，事件用 type / detail_type
    /// </summary>
    V12
}