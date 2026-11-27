using System.Text.Json.Nodes;

namespace PlugBotSharp.Api.Messages;

/// <summary>
/// 直通消息：内容交给代理解释，不同代理用不同的 Name
/// </summary>
public record RawMessage : BaseMessage
{
    public RawMessage(string name, JsonNode payload)
    {
        Type = MessageType.Raw;
        Name = name;
        Payload = payload;
    }

    /// <summary>
    /// 消息名，例如 qq.send
    /// </summary>
    public string Name { get; init; }
    /// <summary>
    /// 代理自己的消息体
    /// </summary>
    public JsonNode Payload { get; init; }
}
