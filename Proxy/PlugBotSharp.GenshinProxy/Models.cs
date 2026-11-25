using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PlugBotSharp.GenshinProxy;

/// <summary>
/// 一段消息，对应 gsuid_core 的 Message(type, data)
/// </summary>
public record GsMessage
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    [JsonPropertyName("data")]
    public JsonNode? Data { get; set; }

    /// <summary>
    /// 组一段消息
    /// </summary>
    /// <param name="type">段类型</param>
    /// <param name="data">段内容</param>
    public static GsMessage Of(string type, string? data)
    {
        return new GsMessage
        {
            Type = type,
            Data = data is null ? null : JsonValue.Create(data)
        };
    }
}

/// <summary>
/// 平台发给 gsuid_core 的消息
/// </summary>
public record GsMessageReceive
{
    [JsonPropertyName("bot_id")]
    public string BotId { get; set; } = "Bot";
    [JsonPropertyName("bot_self_id")]
    public string BotSelfId { get; set; } = "";
    [JsonPropertyName("msg_id")]
    public string MsgId { get; set; } = "";
    /// <summary>
    /// group / direct / channel / sub_channel
    /// </summary>
    [JsonPropertyName("user_type")]
    public string UserType { get; set; } = "group";
    [JsonPropertyName("group_id")]
    public string? GroupId { get; set; }
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }
    [JsonPropertyName("sender")]
    public Dictionary<string, string> Sender { get; set; } = [];
    [JsonPropertyName("user_pm")]
    public int UserPm { get; set; } = 3;
    [JsonPropertyName("content")]
    public List<GsMessage> Content { get; set; } = [];
}
