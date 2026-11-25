using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Tests;

/// <summary>
/// 实例的创建、启停和删除，以及启用状态由谁维护
/// </summary>
public class InstanceManagerTests : ComponentTestBase
{
    /// <summary>
    /// 创建实例会写 Instance.json，启用状态由 InstanceManager 落盘
    /// </summary>
    [Fact]
    public void Create_WritesManifest_AndPersistsState()
    {
        var proxy = EnableProxy("qq");

        Assert.True(InstanceManager.TryCreate("qq", "主账号", out var uuid, out var loaded, out var error), error);
        Assert.True(loaded, error);

        // 实例定义和实例配置都在 Instances/{实例uuid}
        var folder = Path.Combine(Root, "Instances", uuid.ToString());
        Assert.True(File.Exists(Path.Combine(folder, "Instance.json")));

        var info = InstanceManager.List().Single(x => x.Uuid == uuid);
        Assert.True(info.Enabled);
        Assert.True(StateManager.IsInstanceEnabled(uuid.ToString()));

        // 禁用后状态要落盘，重启后不会又被启用
        Assert.True(InstanceManager.TryDisable(uuid, out error), error);
        Assert.False(StateManager.IsInstanceEnabled(uuid.ToString()));

        Assert.True(InstanceManager.TryEnable(uuid, out error), error);
        Assert.True(StateManager.IsInstanceEnabled(uuid.ToString()));

        Assert.True(InstanceManager.TryRemove(uuid, true, out error), error);
        Assert.False(Directory.Exists(folder));
        Assert.DoesNotContain(InstanceManager.List(), x => x.Uuid == uuid);
    }

    /// <summary>
    /// 代理没有启用时不允许创建实例
    /// </summary>
    [Fact]
    public void Create_RequiresEnabledProxy()
    {
        var assembly = TestComponents.AddProxy("disabled", new FakeProxy());
        assembly.Init();
        assembly.Load();

        Assert.False(InstanceManager.TryCreate("disabled", "x", out _, out _, out var error));
        Assert.True(error.Length > 0);
    }

    /// <summary>
    /// 代理不存在时不能创建实例
    /// </summary>
    [Fact]
    public void Create_RequiresExistingProxy()
    {
        Assert.False(InstanceManager.TryCreate("missing", "x", out _, out _, out _));
    }

    /// <summary>
    /// 加一个可用的代理
    /// </summary>
    private FakeProxy EnableProxy(string id)
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy(id, proxy);

        assembly.Init();
        assembly.Load();

        Assert.True(assembly.Enable());

        return proxy;
    }
}
