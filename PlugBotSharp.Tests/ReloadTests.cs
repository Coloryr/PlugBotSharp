using PlugBotSharp.Core.Components;

namespace PlugBotSharp.Tests;

/// <summary>
/// 重载配置：组件是 Disable → Reload → Enable，实例还多一步组件推配置
/// </summary>
public class ReloadTests : ComponentTestBase
{
    [Fact]
    public void Component_reload_bounces_enable_state()
    {
        var proxy = EnableProxy(out _);

        Assert.True(ComponentManager.ReloadComponent("qq"));

        Assert.Equal(1, proxy.DisableCount);
        Assert.Equal(1, proxy.ReloadCount);
        // 创建时启用一次，重载后再启用一次
        Assert.Equal(2, proxy.EnableCount);
        Assert.Equal(ComponentState.Enabled, ComponentManager.Proxys["qq"].State);
    }

    [Fact]
    public void Component_reload_keeps_disabled_component_disabled()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();

        Assert.True(ComponentManager.ReloadComponent("qq"));

        Assert.Equal(0, proxy.DisableCount);
        Assert.Equal(1, proxy.ReloadCount);
        Assert.Equal(0, proxy.EnableCount);
        Assert.Equal(ComponentState.Loaded, ComponentManager.Proxys["qq"].State);
    }

    [Fact]
    public void Component_reload_requires_a_proxy()
    {
        Assert.False(ComponentManager.ReloadComponent("missing"));
    }

    [Fact]
    public void Instance_reload_runs_disable_component_reload_reload_enable()
    {
        var proxy = EnableProxy(out _);

        Assert.True(InstanceManager.TryCreate("qq", "主账号", out var uuid, out _, out var error), error);

        var instance = proxy.LastInstance!;

        Assert.True(InstanceManager.TryReload(uuid, out error), error);

        // 创建实例时组件已经推过一次配置
        Assert.Equal(1, instance.DisableCount);
        Assert.Equal(2, proxy.InstanceReloadCount);
        Assert.Equal(1, instance.ReloadCount);
        Assert.Equal(2, instance.EnableCount);
        Assert.True(StateManager.IsInstanceEnabled(uuid.ToString()));

        Assert.True(InstanceManager.TryRemove(uuid, true, out error), error);
    }

    [Fact]
    public void Instance_reload_keeps_disabled_instance_disabled()
    {
        var proxy = EnableProxy(out _);

        Assert.True(InstanceManager.TryCreate("qq", "主账号", out var uuid, out _, out var error), error);
        Assert.True(InstanceManager.TryDisable(uuid, out error), error);

        var instance = proxy.LastInstance!;

        Assert.True(InstanceManager.TryReload(uuid, out error), error);

        // TryDisable 已经调用过一次 Disable，重载不再重复停
        Assert.Equal(1, instance.DisableCount);
        Assert.Equal(2, proxy.InstanceReloadCount);
        Assert.Equal(1, instance.ReloadCount);
        Assert.Equal(1, instance.EnableCount);
        Assert.False(StateManager.IsInstanceEnabled(uuid.ToString()));

        Assert.True(InstanceManager.TryRemove(uuid, true, out error), error);
    }

    [Fact]
    public void Instance_reload_requires_an_instance()
    {
        Assert.False(InstanceManager.TryReload(Guid.NewGuid(), out var error));
        Assert.True(error.Length > 0);
    }

    /// <summary>
    /// 加一个启用的代理
    /// </summary>
    private FakeProxy EnableProxy(out ProxyAssembly assembly)
    {
        var proxy = new FakeProxy();
        assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();

        Assert.True(assembly.Enable());

        return proxy;
    }
}