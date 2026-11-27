using PlugBotSharp.Core.Components;
using PlugBotSharp.Core.Http;

namespace PlugBotSharp.Tests;

/// <summary>
/// 组件上报的自定义状态：单值状态和只读键值状态
/// </summary>
public class StateApiTests : ComponentTestBase
{
    [Fact]
    public void Proxy_state_is_registered()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();

        var register = new ComponentRegister(proxy, "qq", "qq");

        register.RegisterState("connected").Set("true");
        register.RegisterState("queue").Set(3);

        var states = StateRegistry.Get(proxy);

        Assert.Equal(StateRegistry.ValueKind, states["connected"].Kind);
        Assert.Equal("true", states["connected"].Value);
        Assert.Equal(StateRegistry.ValueKind, states["queue"].Kind);
        Assert.Equal(3, states["queue"].Value);
    }

    [Fact]
    public void Plugin_state_is_registered()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("demo", plugin);

        assembly.Init();
        assembly.Load();

        new ComponentRegister(plugin, "demo").RegisterState("handled").Set(12);

        Assert.Equal(12, StateRegistry.Get(plugin)["handled"].Value);
    }

    [Fact]
    public void Instance_state_is_registered()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();
        assembly.Enable();

        Assert.True(InstanceManager.TryCreate("qq", "主账号", out var uuid, out _, out var error), error);

        var instance = proxy.LastInstance!;

        new ComponentRegister(instance, "qq", "qq").RegisterState("token").Set("alive");

        Assert.Equal("alive", InstanceManager.GetStates(uuid)["token"].Value);

        Assert.True(InstanceManager.TryRemove(uuid, true, out error), error);
    }

    [Fact]
    public void State_is_cleared_when_the_component_unloads()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();

        new ComponentRegister(proxy, "qq", "qq").RegisterState("connected").Set("true");
        Assert.Single(StateRegistry.Get(proxy));

        assembly.Unload();

        Assert.Empty(StateRegistry.Get(proxy));
    }

    [Fact]
    public void State_is_cleared_when_the_instance_is_removed()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();
        assembly.Enable();

        Assert.True(InstanceManager.TryCreate("qq", "主账号", out var uuid, out _, out var error), error);

        var instance = proxy.LastInstance!;

        new ComponentRegister(instance, "qq", "qq").RegisterState("token").Set("alive");

        Assert.True(InstanceManager.TryRemove(uuid, true, out error), error);
        Assert.Empty(StateRegistry.Get(instance));
    }

    [Fact]
    public void State_can_be_updated_and_removed()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();

        var queue = new ComponentRegister(proxy, "qq", "qq").RegisterState("queue");

        queue.Set(1);
        queue.Set(9);

        Assert.Equal(9, StateRegistry.Get(proxy)["queue"].Value);

        queue.Remove();

        Assert.Empty(StateRegistry.Get(proxy));
    }

    [Fact]
    public void Value_state_keeps_the_primitive_type()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();

        var state = new ComponentRegister(proxy, "qq", "qq").RegisterState("typed");

        state.Set(true);
        Assert.IsType<bool>(StateRegistry.Get(proxy)["typed"].Value);

        state.Set(10L);
        Assert.IsType<long>(StateRegistry.Get(proxy)["typed"].Value);

        state.Set(0.5);
        Assert.IsType<double>(StateRegistry.Get(proxy)["typed"].Value);

        state.Set(7UL);
        Assert.IsType<ulong>(StateRegistry.Get(proxy)["typed"].Value);
    }

    [Fact]
    public void Kv_state_keeps_keys_with_their_values()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();

        var state = new ComponentRegister(proxy, "qq", "qq").RegisterKvState("groups");

        state.Set("group-a", 3);
        state.Set("group-b", 5);
        state.Set("group-a", 4);

        var view = StateRegistry.Get(proxy)["groups"];

        Assert.Equal(StateRegistry.KvKind, view.Kind);

        var items = Assert.IsType<Dictionary<string, object?>>(view.Value);

        Assert.Equal(2, items.Count);
        Assert.Equal(4, items["group-a"]);
        Assert.Equal(5, items["group-b"]);
    }

    [Fact]
    public void Kv_state_keys_can_be_removed_and_cleared()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();

        var state = new ComponentRegister(proxy, "qq", "qq").RegisterKvState("groups");

        state.Set("a", 1);
        state.Set("b", 2);
        state.Remove("a");

        Assert.Single(Assert.IsType<Dictionary<string, object?>>(StateRegistry.Get(proxy)["groups"].Value));

        state.Clear();

        Assert.Empty(Assert.IsType<Dictionary<string, object?>>(StateRegistry.Get(proxy)["groups"].Value));
    }

    [Fact]
    public void Kv_state_is_cleared_when_the_component_unloads()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();

        new ComponentRegister(proxy, "qq", "qq").RegisterKvState("groups").Set("a", 1);

        Assert.Single(StateRegistry.Get(proxy));

        assembly.Unload();

        Assert.Empty(StateRegistry.Get(proxy));
    }

    [Fact]
    public void State_kind_switches_from_value_to_kv()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();

        var register = new ComponentRegister(proxy, "qq", "qq");

        register.RegisterState("mixed").Set("old");
        register.RegisterKvState("mixed").Set("a", 1);

        var view = StateRegistry.Get(proxy)["mixed"];

        Assert.Equal(StateRegistry.KvKind, view.Kind);
        Assert.Single(Assert.IsType<Dictionary<string, object?>>(view.Value));
    }

    [Fact]
    public void State_label_and_description_are_reported()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();

        var register = new ComponentRegister(proxy, "qq", "qq");

        var quota = register.RegisterState("quota", "今日配额", "今天这个群还能发几条主动消息");
        var groups = register.RegisterKvState("groups", "群列表", "各群今天已经发出去的条数");

        Assert.Equal("quota", quota.Name);
        Assert.Equal("今日配额", quota.Label);
        Assert.Equal("今天这个群还能发几条主动消息", quota.Description);
        Assert.Equal("groups", groups.Name);

        // 注册了还没上报，面板上也能看到，值先留空
        var declared = StateRegistry.Get(proxy);

        Assert.Equal("今日配额", declared["quota"].Label);
        Assert.Equal("今天这个群还能发几条主动消息", declared["quota"].Description);
        Assert.Equal(StateRegistry.ValueKind, declared["quota"].Kind);
        Assert.Null(declared["quota"].Value);
        Assert.Equal("群列表", declared["groups"].Label);
        Assert.Empty(Assert.IsType<Dictionary<string, object?>>(declared["groups"].Value));

        quota.Set(3);
        groups.Set("群A", 2);

        var states = StateRegistry.Get(proxy);

        Assert.Equal(3, states["quota"].Value);
        Assert.Equal(2, Assert.IsType<Dictionary<string, object?>>(states["groups"].Value)["群A"]);

        // 不填名字时用状态名顶上
        var plain = register.RegisterState("plain");

        Assert.Equal("plain", plain.Label);
        Assert.Equal("", plain.Description);
    }
}
