using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace PlugBotSharp.Core.Configs;

internal class ConfigObj
{
    /// <summary>
    /// 管理面板端口，为 0 时不启动
    /// </summary>
    public ushort Port { get; set; } = 10000;
}

internal class InstanceObj
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Guid Uuid { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ConfigObj))]
[JsonSerializable(typeof(InstanceObj))]
internal partial class SourceGenerationContext : JsonSerializerContext
{

}