using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;

namespace PlugBotSharp.Tests;

public class StateTests : ComponentTestBase
{
    [Fact]
    public void Component_state_is_persisted()
    {
        StateManager.SetComponentEnabled("x", false);

        Assert.False(StateManager.IsComponentEnabled("x"));

        var json = File.ReadAllText(Path.Combine(Root, "Configs", "ComponentState.json"));
        Assert.Contains("\"x\": false", json);

        StateManager.SetComponentEnabled("x", true);

        Assert.True(StateManager.IsComponentEnabled("x"));
    }

    [Fact]
    public void Unknown_component_is_enabled_by_default()
    {
        Assert.True(StateManager.IsComponentEnabled("never-seen"));
    }

    [Fact]
    public void Instance_state_is_persisted()
    {
        var id = Guid.NewGuid().ToString();

        StateManager.SetInstanceEnabled(id, false);

        Assert.False(StateManager.IsInstanceEnabled(id));

        StateManager.SetInstanceEnabled(id, true);

        Assert.True(StateManager.IsInstanceEnabled(id));

        var json = File.ReadAllText(Path.Combine(Root, "Configs", "InstanceState.json"));
        Assert.Contains(id, json);
    }
}
