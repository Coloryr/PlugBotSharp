using Microsoft.AspNetCore.Http;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Tests;

public class ComponentApiTests : ComponentTestBase
{
    /// <summary>
    /// 模拟 RegisterEventHook 阶段的注册器
    /// </summary>
    private static ComponentRegister Register(object owner)
    {
        return new ComponentRegister(owner, "test");
    }

    [Fact]
    public void RegisterHttpPath_for_plugin_uses_component_id()
    {
        var plugin = new FakePlugin();
        TestComponents.AddPlugin("demo", plugin);

        Func<HttpContext, Task> handler = _ => Task.CompletedTask;

        Register(plugin).RegisterHttpPath(handler);

        Assert.True(ApiRegistry.TryGet("api/demo", out var call));
        Assert.Same(handler, call);

        ApiRegistry.Unregister(plugin);

        Assert.False(ApiRegistry.TryGet("api/demo", out _));
    }

    [Fact]
    public void RegisterHttpPath_for_proxy_uses_component_id()
    {
        var proxy = new FakeProxy();
        TestComponents.AddProxy("qq", proxy);

        Func<HttpContext, Task> handler = _ => Task.CompletedTask;

        Register(proxy).RegisterHttpPath(handler);

        Assert.True(ApiRegistry.TryGet("api/qq", out var call));
        Assert.Same(handler, call);

        ApiRegistry.Unregister(proxy);

        Assert.False(ApiRegistry.TryGet("api/qq", out _));
    }

    [Fact]
    public void RegisterHttpPath_for_instance_throws_when_not_created()
    {
        Assert.Throws<InvalidOperationException>(() => Register(new FakeInstance()).RegisterHttpPath(_ => Task.CompletedTask));
    }

    [Fact]
    public void RegisterHttpPath_throws_when_plugin_not_loaded()
    {
        var plugin = new FakePlugin();

        Assert.Throws<InvalidOperationException>(() => Register(plugin).RegisterHttpPath(_ => Task.CompletedTask));
    }

    [Fact]
    public void UnregisterHttpPath_for_plugin_removes_registration()
    {
        var plugin = new FakePlugin();
        TestComponents.AddPlugin("demo", plugin);

        Register(plugin).RegisterHttpPath(_ => Task.CompletedTask);

        Assert.True(ComponentApi.UnregisterHttpPath(plugin));
        Assert.False(ApiRegistry.TryGet("api/demo", out _));

        // 重复取消不算失败
        Assert.False(ComponentApi.UnregisterHttpPath(plugin));
    }

    [Fact]
    public void UnregisterHttpPath_for_proxy_removes_registration()
    {
        var proxy = new FakeProxy();
        TestComponents.AddProxy("qq", proxy);

        Register(proxy).RegisterHttpPath(_ => Task.CompletedTask);

        Assert.True(ComponentApi.UnregisterHttpPath(proxy));
        Assert.False(ApiRegistry.TryGet("api/qq", out _));
        Assert.False(ComponentApi.UnregisterHttpPath(proxy));
    }

    [Fact]
    public void UnregisterHttpPath_for_instance_removes_registration()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();

        Assert.True(assembly.Enable());

        Assert.True(InstanceManager.TryCreate("qq", "主账号", out var uuid, out _, out var error), error);

        var instance = proxy.LastInstance!;

        Register(instance).RegisterHttpPath(_ => Task.CompletedTask);

        Assert.True(ApiRegistry.TryGet($"api/qq/{uuid}", out _));

        Assert.True(ComponentApi.UnregisterHttpPath(instance));
        Assert.False(ApiRegistry.TryGet($"api/qq/{uuid}", out _));

        Assert.True(InstanceManager.TryRemove(uuid, true, out error), error);
    }

    [Fact]
    public void UnregisterHttpPath_keeps_registration_of_another_owner()
    {
        var first = new FakePlugin();
        var second = new FakePlugin();

        TestComponents.AddPlugin("demo", first);
        Register(first).RegisterHttpPath(_ => Task.CompletedTask);

        // 组件被替换后同一个路径归新实例所有
        TestComponents.AddPlugin("demo", second);
        Func<HttpContext, Task> handler = _ => Task.CompletedTask;
        Register(second).RegisterHttpPath(handler);

        Assert.False(ComponentApi.UnregisterHttpPath(first));

        Assert.True(ApiRegistry.TryGet("api/demo", out var call));
        Assert.Same(handler, call);
    }

    [Fact]
    public void UnregisterHttpPath_works_after_component_is_gone()
    {
        var plugin = new FakePlugin();
        TestComponents.AddPlugin("demo", plugin);
        Register(plugin).RegisterHttpPath(_ => Task.CompletedTask);

        ComponentManager.Plugins.Remove("demo", out _);

        Assert.True(ComponentApi.UnregisterHttpPath(plugin));
        Assert.False(ApiRegistry.TryGet("api/demo", out _));
    }

    [Fact]
    public void Components_can_be_listed_with_their_state()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("demo", plugin);

        assembly.Init();
        assembly.Load();

        var loaded = Assert.Single(ComponentApi.GetComponents());

        Assert.Equal("demo", loaded.Id);
        Assert.Equal(ComponentType.Plugin, loaded.Type);
        Assert.True(loaded.Loaded);
        Assert.False(loaded.Enabled);
        Assert.False(loaded.Blocked);
        Assert.Equal("loaded", loaded.State);

        Assert.True(assembly.Enable());

        var enabled = Assert.Single(ComponentApi.GetComponents());

        Assert.True(enabled.Enabled);
        Assert.Equal("enabled", enabled.State);
    }

    [Fact]
    public void GetComponentState_follows_enable_and_disable()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("demo", plugin);

        assembly.Init();
        assembly.Load();

        Assert.Equal("loaded", ComponentApi.GetComponentState("demo"));

        Assert.True(ComponentApi.StartComponent("demo"));
        Assert.Equal("enabled", ComponentApi.GetComponentState("demo"));

        Assert.True(ComponentApi.DisableComponent("demo"));
        Assert.Equal("disabled", ComponentApi.GetComponentState("demo"));

        Assert.Equal("unloaded", ComponentApi.GetComponentState("missing"));
    }

    [Fact]
    public void StartComponent_returns_false_when_the_component_does_not_exist()
    {
        Assert.False(ComponentApi.StartComponent("missing"));
    }

    [Fact]
    public void ReloadComponent_restores_the_previous_state()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("demo", plugin);

        assembly.Init();
        assembly.Load();
        Assert.True(assembly.Enable());

        Assert.True(ComponentApi.ReloadComponent("demo"));

        Assert.Equal(1, plugin.ReloadCount);
        Assert.Equal("enabled", ComponentApi.GetComponentState("demo"));
    }

    [Fact]
    public void Component_config_can_be_read_and_updated()
    {
        var proxy = new FakeProxy();
        TestComponents.AddProxy("qq", proxy);

        var map = ComponentApi.GetComponentConfig("qq");

        Assert.NotNull(map);
        Assert.False(((ConfigBoolMap)map["Log"]).Value);

        var update = new Dictionary<string, ConfigMap>
        {
            ["Log"] = new ConfigBoolMap { Key = "Log", Value = true }
        };

        Assert.True(ComponentApi.UpdateComponentConfig("qq", update));
        Assert.Same(update, proxy.LastUpdate);

        Assert.Null(ComponentApi.GetComponentConfig("missing"));
    }

    [Fact]
    public void Instances_can_be_listed_enabled_and_disabled()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();
        Assert.True(assembly.Enable());

        Assert.True(InstanceManager.TryCreate("qq", "主账号", out var uuid, out _, out var error), error);

        var created = Assert.Single(ComponentApi.GetInstances(), x => x.Uuid == uuid);

        Assert.Equal("qq", created.Proxy);
        Assert.Equal("主账号", created.Name);
        Assert.True(created.Loaded);
        Assert.True(created.Enabled);

        Assert.True(ComponentApi.DisableInstance(uuid));
        Assert.False(Assert.Single(ComponentApi.GetInstances(), x => x.Uuid == uuid).Enabled);

        Assert.True(ComponentApi.EnableInstance(uuid));
        Assert.True(Assert.Single(ComponentApi.GetInstances(), x => x.Uuid == uuid).Enabled);

        Assert.True(ComponentApi.ReloadInstance(uuid));
        Assert.Equal(1, proxy.LastInstance!.ReloadCount);

        Assert.True(InstanceManager.TryRemove(uuid, true, out error), error);
    }

    [Fact]
    public void Instance_config_can_be_read_and_updated()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();
        Assert.True(assembly.Enable());

        Assert.True(InstanceManager.TryCreate("qq", "主账号", out var uuid, out _, out var error), error);

        var map = ComponentApi.GetInstanceConfig(uuid);

        Assert.NotNull(map);
        Assert.Equal("", ((ConfigStringMap)map["Name"]).Value);

        Assert.True(ComponentApi.UpdateInstanceConfig(uuid, new Dictionary<string, ConfigMap>
        {
            ["Name"] = new ConfigStringMap { Key = "Name", Value = "改名" }
        }));

        Assert.Equal("改名", ((ConfigStringMap)ComponentApi.GetInstanceConfig(uuid)!["Name"]).Value);

        Assert.Null(ComponentApi.GetInstanceConfig(Guid.NewGuid()));

        Assert.True(InstanceManager.TryRemove(uuid, true, out error), error);
    }
}
