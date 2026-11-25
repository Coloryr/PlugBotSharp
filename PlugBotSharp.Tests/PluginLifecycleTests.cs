using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;

namespace PlugBotSharp.Tests;

public class PluginLifecycleTests : ComponentTestBase
{
    [Fact]
    public void Init_is_called_only_once()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("p", plugin);

        Assert.True(assembly.Init());
        Assert.True(assembly.Init());
        Assert.True(assembly.Init());

        Assert.Equal(1, plugin.InitCount);
    }

    [Fact]
    public void Enable_disable_cycle_does_not_init_again()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("p", plugin);

        Assert.True(assembly.Init());
        Assert.True(assembly.Load());

        Assert.True(assembly.Enable());
        Assert.True(assembly.Enable());
        Assert.Equal(1, plugin.EnableCount);

        Assert.True(assembly.Disable());
        Assert.Equal(1, plugin.DisableCount);

        Assert.True(assembly.Enable());
        Assert.Equal(2, plugin.EnableCount);
        Assert.Equal(1, plugin.InitCount);
    }

    [Fact]
    public void Enable_before_load_returns_false()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("p", plugin);

        Assert.True(assembly.Init());
        Assert.False(assembly.Enable());
        Assert.Equal(0, plugin.EnableCount);
    }

    [Fact]
    public void Unload_marks_state_and_calls_plugin()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("p", plugin);

        Assert.True(assembly.Init());
        Assert.True(assembly.Load());
        Assert.True(assembly.Unload());

        Assert.Equal(1, plugin.UnloadCount);
        Assert.Equal(ComponentState.Unloaded, assembly.State);
    }

    [Fact]
    public void Failed_init_keeps_state_none()
    {
        var plugin = new FakePlugin { InitResult = false };
        var assembly = TestComponents.AddPlugin("p", plugin);

        Assert.False(assembly.Init());
        Assert.Equal(ComponentState.None, assembly.State);
    }
}
