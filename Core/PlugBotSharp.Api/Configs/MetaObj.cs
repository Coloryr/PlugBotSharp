using System.Text.Json.Serialization;

namespace PlugBotSharp.Core.Configs;

[JsonConverter(typeof(JsonStringEnumConverter<ComponentType>))]
public enum ComponentType
{
    Plugin, Proxy, Lib
}

[JsonConverter(typeof(JsonStringEnumConverter<DepType>))]
public enum DepType
{
    Component, Nuget
}

public record DepInfoObj
{
    public string Id { get; set; }
    public DepType Type { get; set; }
    public string Version { get; set; }
}

/// <summary>
/// 组件信息
/// </summary>
public record MetaObj
{
    public string Name { get; set; }
    public string Id { get; set; }
    public string Version { get; set; }
    public string Description { get; set; }
    public List<string> Dlls { get; set; }
    public ComponentType Type { get; set; }
    public List<DepInfoObj> Deps { get; set; }
}
