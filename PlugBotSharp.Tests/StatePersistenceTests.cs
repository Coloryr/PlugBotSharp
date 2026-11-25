using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;

namespace PlugBotSharp.Tests;

/// <summary>
/// 状态落盘：宿主自动把状态写到组件的 Configs/{id}/states.json，重启后读回来
/// </summary>
public class StatePersistenceTests : ComponentTestBase
{
    [Fact]
    public void Value_state_is_saved_and_restored()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("demo", plugin);

        assembly.Init();
        assembly.Load();

        var state = new ComponentRegister(plugin, "demo").RegisterState("replies", "回复条数", "累计条数");

        state.Set(12);

        StateRegistry.Flush();

        var file = Path.Combine(Root, "Configs", "demo", "states.json");

        Assert.True(File.Exists(file));
        Assert.Contains("\"replies\"", File.ReadAllText(file));

        // 换一个组件对象，模拟重启后重新加载
        var again = new FakePlugin();
        var assembly2 = TestComponents.AddPlugin("demo", again);

        assembly2.Init();
        assembly2.Load();

        var restored = new ComponentRegister(again, "demo").RegisterState("replies", "回复条数", "累计条数");

        Assert.Equal(12L, restored.Get());
    }

    [Fact]
    public void Kv_state_is_saved_and_restored()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("demo", plugin);

        assembly.Init();
        assembly.Load();

        var state = new ComponentRegister(plugin, "demo").RegisterKvState("groups", "群计数", "各群条数");

        state.Set("群A", 3);
        state.Set("群B", 5);

        StateRegistry.Flush();

        var again = new FakePlugin();
        var assembly2 = TestComponents.AddPlugin("demo", again);

        assembly2.Init();
        assembly2.Load();

        var restored = new ComponentRegister(again, "demo").RegisterKvState("groups", "群计数", "各群条数");
        var items = restored.Get();

        Assert.Equal(2, items.Count);
        Assert.Equal(3L, items["群A"]);
        Assert.Equal(5L, items["群B"]);
    }

    [Fact]
    public void Save_interval_has_a_one_second_floor()
    {
        try
        {
            StateRegistry.SetSaveInterval(200);
            Assert.Equal(1000, StateRegistry.SaveInterval);

            StateRegistry.SetSaveInterval(5000);
            Assert.Equal(5000, StateRegistry.SaveInterval);
        }
        finally
        {
            StateRegistry.SetSaveInterval(2000);
        }
    }

    [Fact]
    public void Unloading_flushes_pending_changes()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("demo", plugin);

        assembly.Init();
        assembly.Load();

        var state = new ComponentRegister(plugin, "demo").RegisterState("sent");

        state.Set(7);

        // 组件卸载会移除状态，移之前要写盘
        assembly.Unload();

        var file = Path.Combine(Root, "Configs", "demo", "states.json");

        Assert.True(File.Exists(file));
        Assert.Contains("\"sent\"", File.ReadAllText(file));
    }
}
