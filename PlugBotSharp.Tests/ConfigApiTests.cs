using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Tests;

/// <summary>
/// 组件和实例的配置项读取、保存，面板设置对话框靠这两个接口
/// </summary>
public class ConfigApiTests : ComponentTestBase
{
    [Fact]
    public void Component_config_comes_from_the_proxy()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();
        assembly.Enable();

        Assert.True(ComponentManager.TryGetConfigMap("qq", out var map));
        Assert.IsType<ConfigBoolMap>(map["Log"]);

        var data = new Dictionary<string, ConfigMap>
        {
            ["Log"] = new ConfigBoolMap { Key = "Log", Value = true }
        };

        Assert.True(ComponentManager.TryUpdateConfigMap("qq", data));
        Assert.Same(data, proxy.LastUpdate);
    }

    [Fact]
    public void Component_config_is_missing_for_unknown_component()
    {
        Assert.False(ComponentManager.TryGetConfigMap("missing", out var map));
        Assert.Empty(map);
        Assert.False(ComponentManager.TryUpdateConfigMap("missing", []));
    }

    [Fact]
    public void Instance_config_round_trips_through_the_manager()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();
        assembly.Enable();

        Assert.True(InstanceManager.TryCreate("qq", "主账号", out var uuid, out _, out var error), error);

        Assert.True(InstanceManager.TryGetConfigMap(uuid, out var map));
        Assert.IsType<ConfigStringMap>(map["Name"]);

        Assert.True(InstanceManager.TryUpdateConfigMap(uuid, new Dictionary<string, ConfigMap>
        {
            ["Name"] = new ConfigStringMap { Key = "Name", Value = "hello" }
        }));

        Assert.Equal("hello", proxy.LastInstance!.Name);
        Assert.True(InstanceManager.TryRemove(uuid, true, out error), error);
    }

    [Fact]
    public void Instance_config_is_missing_for_unknown_instance()
    {
        Assert.False(InstanceManager.TryGetConfigMap(Guid.NewGuid(), out _));
        Assert.False(InstanceManager.TryUpdateConfigMap(Guid.NewGuid(), []));
    }
}
