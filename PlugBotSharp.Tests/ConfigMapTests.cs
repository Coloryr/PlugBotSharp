using System.Text.Json;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Tests;

/// <summary>
/// 配置项定义的序列化，网页端靠 type 字段决定用哪种控件
/// </summary>
public class ConfigMapTests
{
    private static List<ConfigMap> Sample()
    {
        return
        [
            new ConfigIntMap { Key = "port", Value = 10000, Min = 1, Max = 65535, Default = 10000 },
            new ConfigFloatMap { Key = "ratio", Value = 0.5, Min = 0, Max = 1, Default = 0.25 },
            new ConfigBoolMap { Key = "enable", Value = true, Default = false },
            new ConfigStringMap { Key = "secret", Value = "abc", Default = "", MaxLength = 32, Secret = true },
            new ConfigListMap { Key = "white", Value = ["a", "b"], Default = [], MaxCount = 10 },
            new ConfigKvMap { Key = "headers", Value = { ["x"] = "1" }, Default = [] },
            new ConfigEnumMap { Key = "type", Value = "WebHook", Default = "WebHook", Options = ["WebHook", "WebSocket"] }
        ];
    }

    [Fact]
    public void Serialize_writes_type_and_constraints()
    {
        var json = JsonSerializer.Serialize(Sample(), ConfigMapContext.Default.ListConfigMap);
        var list = JsonSerializer.Deserialize(json, ConfigMapContext.Default.ListConfigMap)!;

        Assert.Equal(7, list.Count);

        var port = Assert.IsType<ConfigIntMap>(list[0]);
        Assert.Equal("port", port.Key);
        Assert.Equal(10000, port.Value);
        Assert.Equal(65535, port.Max);

        var ratio = Assert.IsType<ConfigFloatMap>(list[1]);
        Assert.Equal(0.5, ratio.Value);

        var enable = Assert.IsType<ConfigBoolMap>(list[2]);
        Assert.True(enable.Value);

        var secret = Assert.IsType<ConfigStringMap>(list[3]);
        Assert.True(secret.Secret);
        Assert.Equal(32, secret.MaxLength);

        var white = Assert.IsType<ConfigListMap>(list[4]);
        Assert.Equal(["a", "b"], white.Value);

        var headers = Assert.IsType<ConfigKvMap>(list[5]);
        Assert.Equal("1", headers.Value["x"]);

        var type = Assert.IsType<ConfigEnumMap>(list[6]);
        Assert.Equal("WebHook", type.Value);
        Assert.Equal(["WebHook", "WebSocket"], type.Options);
    }

    [Fact]
    public void Serialize_uses_lowercase_type_names()
    {
        var json = JsonSerializer.Serialize(Sample(), ConfigMapContext.Default.ListConfigMap);

        Assert.Contains("\"type\": \"int\"", json);
        Assert.Contains("\"key\": \"port\"", json);
        Assert.Contains("\"maxLength\": 32", json);
        Assert.Contains("\"type\": \"float\"", json);
        Assert.Contains("\"type\": \"bool\"", json);
        Assert.Contains("\"type\": \"string\"", json);
        Assert.Contains("\"type\": \"list\"", json);
        Assert.Contains("\"type\": \"kv\"", json);
        Assert.Contains("\"type\": \"enum\"", json);
    }

    [Fact]
    public void Dictionary_round_trips_by_key()
    {
        var map = new Dictionary<string, ConfigMap> { ["LogPayload"] = Sample()[2] };
        var json = JsonSerializer.Serialize(map, ConfigMapContext.Default.DictionaryStringConfigMap);
        var back = JsonSerializer.Deserialize(json, ConfigMapContext.Default.DictionaryStringConfigMap)!;

        Assert.True(Assert.IsType<ConfigBoolMap>(back["LogPayload"]).Value);
    }

    [Fact]
    public void Reflection_serialization_keeps_the_panel_shape()
    {
        // 面板接口用 Results.Json，走的是反射序列化加 camelCase
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize<object>(new Dictionary<string, ConfigMap>
        {
            ["LogPayload"] = Sample()[2]
        }, options);

        Assert.Contains("\"type\":\"bool\"", json);
        Assert.Contains("\"key\":\"enable\"", json);
        Assert.Contains("\"value\":true", json);
    }

    [Fact]
    public void Deserialize_defaults_missing_collections()
    {
        var json = """[{"type":"list","key":"x"},{"type":"kv","key":"y"}]""";
        var list = JsonSerializer.Deserialize(json, ConfigMapContext.Default.ListConfigMap)!;

        Assert.Empty(Assert.IsType<ConfigListMap>(list[0]).Value);
        Assert.Empty(Assert.IsType<ConfigKvMap>(list[1]).Value);
    }
}