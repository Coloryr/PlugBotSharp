using System.Text.Json.Serialization;

namespace PlugBotSharp.GenshinProxy;

/// <summary>
/// gsuid_core 协议的序列化上下文
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GsMessage))]
[JsonSerializable(typeof(GsMessageReceive))]
public partial class GenshinJsonContext : JsonSerializerContext
{

}
