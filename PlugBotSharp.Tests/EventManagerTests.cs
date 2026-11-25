using System.Text.Json.Nodes;
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
            EventManager.SubscribeMessage(owner, _ => [new TextMessage("pong")]);

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
            EventManager.SubscribeMessage(first, _ => [new TextMessage("a")]);
            EventManager.SubscribeMessage(second, _ => [new TextMessage("b")]);

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
    public async Task Async_message_hook_does_not_block_and_can_reply_later()
    {
        var plugin = new object();
        var sender = new object();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        BaseMessage? replied = null;

        try
        {
            EventManager.SubscribeMessage(plugin, async evt =>
            {
                await gate.Task;

                await evt.Reply(new TextMessage("later"));
            });

            EventManager.RegisterSendTarget(sender, (_, msg) =>
            {
                replied = msg;

                return Task.CompletedTask;
            });

            var replies = EventManager.HandleMessage("qq", Guid.NewGuid(), new TextMessage("hi"));

            // 长任务没结束，分发已经返回
            Assert.Empty(replies);
            Assert.Null(replied);

            gate.SetResult(true);

            for (var i = 0; i < 100 && replied is null; i++)
            {
                await Task.Delay(20);
            }

            Assert.Equal("later", Assert.IsType<TextMessage>(replied!).Text);
        }
        finally
        {
            EventManager.Unregister(plugin);
            EventManager.Unregister(sender);
        }
    }

    [Fact]
    public async Task Broken_async_message_hook_is_ignored()
    {
        var plugin = new object();

        try
        {
            EventManager.SubscribeMessage(plugin, (Func<MessageEvent, Task>)(_ => throw new InvalidOperationException("boom")));

            var replies = EventManager.HandleMessage("qq", Guid.Empty, new TextMessage("hi"));

            Assert.Empty(replies);

            await Task.Delay(200);
        }
        finally
        {
            EventManager.Unregister(plugin);
        }
    }

    [Fact]
    public void Broken_message_hook_does_not_stop_the_others()
    {
        var broken = new object();
        var healthy = new object();

        try
        {
            EventManager.SubscribeMessage(broken, (Func<ProxyMessage, List<BaseMessage>>)(_ => throw new InvalidOperationException("boom")));
            EventManager.SubscribeMessage(healthy, _ => [new TextMessage("ok")]);

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

        EventManager.SubscribeMessage(owner, _ => [new TextMessage("x")]);
        EventManager.SubscribeProxy(owner, _ => { });
        EventManager.SubscribePlugin(owner, _ => { });

        Assert.True(EventManager.Unregister(owner));
        Assert.Empty(EventManager.HandleMessage("qq", Guid.Empty, new TextMessage("x")));
        Assert.False(EventManager.Unregister(owner));
    }

    [Fact]
    public async Task Send_hook_receives_the_message()
    {
        var owner = new object();
        ProxyMessage? received = null;
        BaseMessage? body = null;

        try
        {
            EventManager.RegisterSendTarget(owner, (ctx, msg) =>
            {
                received = ctx;
                body = msg;

                return Task.CompletedTask;
            });

            var context = new ProxyMessage
            {
                Id = "qq",
                Uuid = Guid.NewGuid(),
                SenderType = MessageSenderType.Group,
                Sender = "user",
                Target = "group",
                Message = new TextMessage("x")
            };

            await EventManager.HandleSendAsync(context, new TextMessage("hi"));

            Assert.Same(context, received);
            Assert.Equal("hi", Assert.IsType<TextMessage>(body).Text);
        }
        finally
        {
            EventManager.Unregister(owner);
        }
    }

    [Fact]
    public async Task Send_reaches_the_instance_by_uuid()
    {
        var proxy = new FakeProxy();
        var assembly = TestComponents.AddProxy("qq", proxy);

        assembly.Init();
        assembly.Load();
        assembly.Enable();

        Assert.True(InstanceManager.TryCreate("qq", "主账号", out var uuid, out _, out var error), error);

        var instance = proxy.LastInstance!;

        try
        {
            await EventManager.HandleSendAsync(new ProxyMessage
            {
                Id = "qq",
                Uuid = uuid,
                SenderType = MessageSenderType.Group,
                Sender = "user",
                Target = "group",
                Message = new TextMessage("x")
            }, new TextMessage("hi"));

            Assert.Equal("hi", Assert.IsType<TextMessage>(instance.LastReply).Text);
        }
        finally
        {
            Assert.True(InstanceManager.TryRemove(uuid, true, out error), error);
        }
    }

    [Fact]
    public async Task Send_is_canceled_by_an_interceptor()
    {
        var sender = new object();
        var filter = new object();
        var sent = 0;

        try
        {
            EventManager.RegisterSendTarget(sender, (_, _) =>
            {
                sent++;

                return Task.CompletedTask;
            });

            EventManager.SubscribeSendFilter(filter, (_, _) => Task.FromResult(true));

            await EventManager.HandleSendAsync(Context(), new TextMessage("hi"));

            Assert.Equal(0, sent);
        }
        finally
        {
            EventManager.Unregister(sender);
            EventManager.Unregister(filter);
        }
    }

    [Fact]
    public async Task Interceptor_can_let_the_message_through()
    {
        var sender = new object();
        var filter = new object();
        var sent = 0;

        try
        {
            EventManager.RegisterSendTarget(sender, (_, _) =>
            {
                sent++;

                return Task.CompletedTask;
            });

            EventManager.SubscribeSendFilter(filter, (_, _) => Task.FromResult(false));

            await EventManager.HandleSendAsync(Context(), new TextMessage("hi"));

            Assert.Equal(1, sent);
        }
        finally
        {
            EventManager.Unregister(sender);
            EventManager.Unregister(filter);
        }
    }

    [Fact]
    public async Task Broken_interceptor_does_not_block_the_message()
    {
        var sender = new object();
        var filter = new object();
        var sent = 0;

        try
        {
            EventManager.RegisterSendTarget(sender, (_, _) =>
            {
                sent++;

                return Task.CompletedTask;
            });

            EventManager.SubscribeSendFilter(filter, (_, _) => throw new InvalidOperationException("boom"));

            await EventManager.HandleSendAsync(Context(), new TextMessage("hi"));

            Assert.Equal(1, sent);
        }
        finally
        {
            EventManager.Unregister(sender);
            EventManager.Unregister(filter);
        }
    }

    /// <summary>
    /// 造一个发送上下文
    /// </summary>
    private static ProxyMessage Context()
    {
        return new ProxyMessage
        {
            Id = "qq",
            Uuid = Guid.NewGuid(),
            SenderType = MessageSenderType.Group,
            Sender = "user",
            Target = "group",
            Message = new TextMessage("x")
        };
    }

    [Fact]
    public async Task Send_hook_is_removed_with_the_owner()
    {
        var owner = new object();
        var count = 0;

        EventManager.RegisterSendTarget(owner, (_, _) =>
        {
            count++;
            return Task.CompletedTask;
        });

        var context = new ProxyMessage
        {
            Id = "qq",
            Uuid = Guid.Empty,
            SenderType = MessageSenderType.User,
            Sender = "user",
            Message = new TextMessage("x")
        };

        await EventManager.HandleSendAsync(context, new TextMessage("hi"));
        Assert.Equal(1, count);

        EventManager.Unregister(owner);

        await EventManager.HandleSendAsync(context, new TextMessage("hi"));
        Assert.Equal(1, count);
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
            EventManager.SubscribePlugin(owner, e => seen.Add(e));

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
            EventManager.SubscribePlugin(owner, e => received = e);

            EventManager.RaisePlugin("demo", "ping", JsonNode.Parse("""{"value":42}"""));

            Assert.NotNull(received);
            Assert.Equal(PluginEventType.Custom, received!.Type);
            Assert.Equal("ping", received.Name);
            Assert.Equal(42, Assert.IsType<JsonObject>(received.Data)["value"]!.GetValue<int>());
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
            EventManager.SubscribeProxy(owner, e => seen.Add(e));

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
        EventManager.SubscribeMessage(plugin, _ =>
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
