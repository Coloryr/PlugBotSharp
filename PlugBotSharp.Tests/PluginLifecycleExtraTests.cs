using PlugBotSharp.Core;

namespace PlugBotSharp.Tests;

/// <summary>
/// 插件也走 IBaseComponent，重载和事件注册钩子的行为要和代理一致
/// </summary>
public class PluginLifecycleExtraTests : ComponentTestBase
{
    [Fact]
    public void Enabling_a_plugin_registers_event_hooks()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("demo", plugin);

        assembly.Init();
        assembly.Load();

        Assert.True(assembly.Enable());
        Assert.Equal(1, plugin.HookCount);
    }

    [Fact]
    public void Plugin_reload_bounces_enable_state()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("demo", plugin);

        assembly.Init();
        assembly.Load();
        assembly.Enable();

        Assert.True(ComponentManager.ReloadComponent("demo"));

        Assert.Equal(1, plugin.DisableCount);
        Assert.Equal(1, plugin.ReloadCount);
        Assert.Equal(2, plugin.EnableCount);
        Assert.Equal(ComponentState.Enabled, ComponentManager.Plugins["demo"].State);
    }

    [Fact]
    public void Plugin_config_map_is_exposed_to_the_panel()
    {
        var plugin = new FakePlugin { Name = "demo" };
        var assembly = TestComponents.AddPlugin("demo", plugin);

        assembly.Init();
        assembly.Load();
        assembly.Enable();

        Assert.True(ComponentManager.TryGetConfigMap("demo", out var map));
        Assert.IsType<PlugBotSharp.Core.Configs.ConfigStringMap>(map["Name"]);
    }
}
