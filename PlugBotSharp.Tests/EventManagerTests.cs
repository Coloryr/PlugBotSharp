using PlugBotSharp.Core;
using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.Tests;

/// <summary>
/// 事件中心：消息事件、Proxy 事件、插件事件
/// </summary>
public class EventManagerTests : ComponentTestBase
{
    [Fact]
    public void Message_hook_returns_replies()
    {
        var owner = new object();

        try
        {
            EventManager.RegisterMessageHook(owner, _ => [new TextMessage("pong")]);

            var replies = EventManager.HandleMessage("qq", Guid.NewGuid(), new TextMessage("ping"));

            Assert.Equal("pong", Assert.IsType<TextMessage>(Assert.Single(replies)).Text);
        }
        finally
        {
            EventManager.Unregister(owner);
        }
    }

    [Fact]
    public void Replies_from_all_hooks_are_merged()
    {
        var first = new object();
        var second = new object();

        try
        {
            EventManager.RegisterMessageHook(first, _ => [new TextMessage("a")]);
            EventManager.RegisterMessageHook(second, _ => [new TextMessage("b")]);

            var replies = EventManager.HandleMessage("qq", Guid.NewGuid(), new TextMessage("x"));

            Assert.Equal(["a", "b"], replies.Cast<TextMessage>().Select(x => x.Text).ToArray());
        }
        finally
        {
            EventManager.Unregister(first);
            EventManager.Unregister(second);
        }
    }

    [Fact]
    public void Broken_message_hook_does_not_stop_the_others()
    {
        var broken = new object();
        var healthy = new object();

        try
        {
            EventManager.RegisterMessageHook(broken, _ => throw new InvalidOperationException("boom"));
            EventManager.RegisterMessageHook(healthy, _ => [new TextMessage("ok")]);

            var replies = EventManager.HandleMessage("qq", Guid.Empty, new TextMessage("x"));

            Assert.Single(replies);
        }
        finally
        {
            EventManager.Unregister(broken);
            EventManager.Unregister(healthy);
        }
    }

    [Fact]
    public void Unregister_removes_every_channel()
    {
        var owner = new object();

        EventManager.RegisterMessageHook(owner, _ => [new TextMessage("x")]);
        EventManager.RegisterProxyHook(owner, _ => { });
        EventManager.RegisterPluginHook(owner, _ => { });

        Assert.True(EventManager.Unregister(owner));
        Assert.Empty(EventManager.HandleMessage("qq", Guid.Empty, new TextMessage("x")));
        Assert.False(EventManager.Unregister(owner));
    }

    [Fact]
    public void Plugin_lifecycle_events_are_raised()
    {
        var owner = new object();
        var seen = new List<PluginEvent>();

        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("demo", plugin);

        try
        {
            EventManager.RegisterPluginHook(owner, e => seen.Add(e));

            assembly.Init();
            assembly.Load();
            assembly.Enable();
            assembly.Disable();
            assembly.Unload();

            Assert.Equal(
                new[] { PluginEventType.Loaded, PluginEventType.Enabled, PluginEventType.Disabled, PluginEventType.Unloaded },
                seen.Select(x => x.Type).ToArray());

            Assert.All(seen, x => Assert.Equal("demo", x.Id));
        }
        finally
        {
            EventManager.Unregister(owner);
        }
    }

    [Fact]
    public void Custom_plugin_event_carries_name_and_data()
    {
        var owner = new object();
        PluginEvent? received = null;

        try
        {
            EventManager.RegisterPluginHook(owner, e => received = e);

            EventManager.RaisePlugin("demo", "ping", 42);

            Assert.NotNull(received);
            Assert.Equal(PluginEventType.Custom, received!.Type);
            Assert.Equal("ping", received.Name);
            Assert.Equal(42, received.Data);
        }
        finally
        {
            EventManager.Unregister(owner);
        }
    }

    [Fact]
    public void Instance_start_and_stop_raise_proxy_events()
    {
        var owner = new object();
        var seen = new List<ProxyEvent>();

        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();
        assembly.Enable();

        try
        {
            EventManager.RegisterProxyHook(owner, e => seen.Add(e));

            Assert.True(InstanceManager.TryCreate("qq", "主账号", out var uuid, out _, out var error), error);

            var started = Assert.Single(seen);
            Assert.Equal(ProxyEventType.Started, started.Type);
            Assert.Equal(uuid, started.Uuid);
            Assert.Equal("qq", started.Id);

            Assert.True(InstanceManager.TryDisable(uuid, out error), error);

            Assert.Equal(2, seen.Count);
            Assert.Equal(ProxyEventType.Stopped, seen[1].Type);

            Assert.True(InstanceManager.TryRemove(uuid, true, out error), error);
        }
        finally
        {
            EventManager.Unregister(owner);
        }
    }

    [Fact]
    public void Unloading_a_plugin_drops_its_hooks()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("demo", plugin);

        assembly.Init();
        assembly.Load();

        var replies = 0;
        EventManager.RegisterMessageHook(plugin, _ =>
        {
            replies++;
            return [];
        });

        EventManager.HandleMessage("qq", Guid.Empty, new TextMessage("x"));
        Assert.Equal(1, replies);

        assembly.Unload();

        EventManager.HandleMessage("qq", Guid.Empty, new TextMessage("x"));
        Assert.Equal(1, replies);
    }
}
