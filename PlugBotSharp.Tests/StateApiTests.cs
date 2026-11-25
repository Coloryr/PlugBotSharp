using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;

namespace PlugBotSharp.Tests;

/// <summary>
/// 组件上报的自定义状态
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
        register.RegisterState("queue").Set("3");

        var states = StateRegistry.Get(proxy);

        Assert.Equal("true", states["connected"]);
        Assert.Equal("3", states["queue"]);
    }

    [Fact]
    public void Plugin_state_is_registered()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("demo", plugin);

        assembly.Init();
        assembly.Load();

        new ComponentRegister(plugin, "demo").RegisterState("handled").Set("12");

        Assert.Equal("12", StateRegistry.Get(plugin)["handled"]);
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

        Assert.Equal("alive", InstanceManager.GetStates(uuid)["token"]);

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

        queue.Set("1");
        queue.Set("9");

        Assert.Equal("9", StateRegistry.Get(proxy)["queue"]);

        queue.Remove();

        Assert.Empty(StateRegistry.Get(proxy));
    }
}
