using PlugBotSharp.Core;

namespace PlugBotSharp.Tests;

public class ComponentManagerTests : ComponentTestBase
{
    [Fact]
    public void UnloadComponent_unloads_dependents_first()
    {
        var a = new FakePlugin { Name = "a" };
        var b = new FakePlugin { Name = "b" };
        var c = new FakePlugin { Name = "c" };

        var pa = TestComponents.AddPlugin("a", a);
        var pb = TestComponents.AddPlugin("b", b, ("a", ">=1.0.0"));
        var pc = TestComponents.AddPlugin("c", c, ("b", ">=1.0.0"));

        foreach (var assembly in new[] { pa, pb, pc })
        {
            Assert.True(assembly.Init());
            Assert.True(assembly.Load());
        }

        Assert.True(ComponentManager.UnloadComponent("a"));

        Assert.Empty(ComponentManager.Plugins);
        Assert.Equal(["c", "b", "a"], FakePlugin.UnloadOrder);
        Assert.Equal(1, a.UnloadCount);
        Assert.Equal(1, b.UnloadCount);
        Assert.Equal(1, c.UnloadCount);
    }

    [Fact]
    public void UnloadComponent_unknown_id_returns_false()
    {
        Assert.False(ComponentManager.UnloadComponent("missing"));
    }

    [Fact]
    public void Blocked_component_cannot_be_enabled()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("blocked", plugin);

        Assert.True(assembly.Init());
        Assert.True(assembly.Load());

        ComponentManager.BlockComponent("blocked");

        Assert.True(ComponentManager.IsComponentBlocked("blocked"));
        Assert.False(ComponentManager.EnableComponent("blocked"));
        Assert.Equal(0, plugin.EnableCount);
    }

    [Fact]
    public void DisableComponent_updates_state()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("p", plugin);

        Assert.True(assembly.Init());
        Assert.True(assembly.Load());
        Assert.True(ComponentManager.EnableComponent("p"));

        Assert.True(ComponentManager.DisableComponent("p"));

        Assert.Equal(1, plugin.DisableCount);
        Assert.False(StateManager.IsComponentEnabled("p"));
    }

    [Fact]
    public void UnloadAll_removes_every_component()
    {
        var pa = TestComponents.AddPlugin("a", new FakePlugin { Name = "a" });
        var pb = TestComponents.AddPlugin("b", new FakePlugin { Name = "b" }, ("a", ">=1.0.0"));

        foreach (var assembly in new[] { pa, pb })
        {
            Assert.True(assembly.Init());
            Assert.True(assembly.Load());
        }

        ComponentManager.UnloadAll();

        Assert.Empty(ComponentManager.Plugins);
        Assert.Equal(["b", "a"], FakePlugin.UnloadOrder);
    }
}
