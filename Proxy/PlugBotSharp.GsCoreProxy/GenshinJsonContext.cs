using System.Text.Json.Serialization;

namespace PlugBotSharp.GsCoreProxy;

/// <summary>
/// gsuid_core 协议的序列化上下文
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GsMessage))]
[JsonSerializable(typeof(List<GsMessage>))]
[JsonSerializable(typeof(GsMessageReceive))]
public partial class GenshinJsonContext : JsonSerializerContext
{

}
