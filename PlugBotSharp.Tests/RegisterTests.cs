using System.Text.Json.Nodes;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.Tests;

/// <summary>
/// RegisterEventHook 阶段拿到的注册器
/// </summary>
public class RegisterTests : ComponentTestBase
{
    [Fact]
    public void Custom_event_name_is_prefixed_with_the_component_id()
    {
        var plugin = new FakePlugin();
        TestComponents.AddPlugin("demo", plugin);

        var register = new ComponentRegister(plugin, "demo");
        var handle = register.RegisterPluginEvent("ping");

        Assert.Equal("demo", handle.Id);
        Assert.Equal("[demo]ping", handle.Name);
    }

    [Fact]
    public void Raising_the_handle_reaches_plugin_hooks()
    {
        var plugin = new FakePlugin();
        var owner = new object();
        var register = new ComponentRegister(plugin, "demo");
        var handle = register.RegisterPluginEvent("ping");

        PluginEvent? received = null;

        try
        {
            EventManager.SubscribePlugin(owner, e => received = e);

            handle.Raise(JsonNode.Parse("""{"value":42}"""));

            Assert.NotNull(received);
            Assert.Equal("[demo]ping", received!.Name);
            Assert.Equal(42, Assert.IsType<JsonObject>(received.Data)["value"]!.GetValue<int>());
        }
        finally
        {
            EventManager.Unregister(owner);
        }
    }

    [Fact]
    public void Subscribing_a_custom_event_only_receives_that_event()
    {
        var plugin = new FakePlugin();
        var register = new ComponentRegister(plugin, "demo");
        var ping = register.RegisterPluginEvent("ping");
        var pong = register.RegisterPluginEvent("pong");

        var received = new List<string>();

        try
        {
            register.SubscribePluginEvent(ping.Name, e => received.Add(e.Name));

            ping.Raise();
            pong.Raise();

            Assert.Equal([ping.Name], received);
        }
        finally
        {
            EventManager.Unregister(plugin);
        }
    }

    [Fact]
    public void Subscribing_with_component_and_event_ids_uses_the_full_name()
    {
        var plugin = new FakePlugin();
        var register = new ComponentRegister(plugin, "demo");
        var ping = register.RegisterPluginEvent("ping");

        PluginEvent? received = null;

        try
        {
            register.SubscribePluginEvent("demo", "ping", e => received = e);

            ping.Raise(7);

            Assert.NotNull(received);
            Assert.Equal("[demo]ping", received!.Name);
            Assert.Equal("demo", received.Id);
            Assert.Equal(7, received.Data);
        }
        finally
        {
            EventManager.Unregister(plugin);
        }
    }

    [Fact]
    public void Named_subscription_ignores_lifecycle_events()
    {
        var plugin = new FakePlugin();
        var register = new ComponentRegister(plugin, "demo");

        var received = new List<PluginEventType>();

        try
        {
            register.SubscribePluginEvent("demo", "ping", e => received.Add(e.Type));

            EventManager.HandlePlugin(new PluginEvent { Id = "demo", Type = PluginEventType.Loaded });
            EventManager.HandlePlugin(new PluginEvent { Id = "demo", Type = PluginEventType.Enabled });

            Assert.Empty(received);
        }
        finally
        {
            EventManager.Unregister(plugin);
        }
    }

    [Fact]
    public void Broadcast_subscription_still_receives_everything()
    {
        var plugin = new FakePlugin();
        var register = new ComponentRegister(plugin, "demo");
        var ping = register.RegisterPluginEvent("ping");

        var received = new List<string>();

        try
        {
            register.SubscribePlugin(e => received.Add(e.Name));

            EventManager.HandlePlugin(new PluginEvent { Id = "demo", Type = PluginEventType.Loaded });
            ping.Raise();

            Assert.Equal(["", "[demo]ping"], received);
        }
        finally
        {
            EventManager.Unregister(plugin);
        }
    }

    [Fact]
    public void Primitive_payloads_keep_their_type()
    {
        var plugin = new FakePlugin();
        var register = new ComponentRegister(plugin, "demo");
        var handle = register.RegisterPluginEvent("ping");

        var received = new List<PluginEvent>();

        try
        {
            register.SubscribePluginEvent("demo", "ping", received.Add);

            handle.Raise("hi");
            handle.Raise(42);
            handle.Raise(true);
            handle.Raise(1.5);
            handle.Raise(new JsonObject { ["value"] = 1 });
            handle.Raise(new TextMessage("hi"));
            handle.Raise();

            Assert.Equal(7, received.Count);
            Assert.Equal("hi", received[0].Data);
            Assert.Equal(42, Assert.IsType<int>(received[1].Data));
            Assert.True(Assert.IsType<bool>(received[2].Data));
            Assert.Equal(1.5, Assert.IsType<double>(received[3].Data));
            Assert.Equal(1, Assert.IsType<JsonObject>(received[4].Data)["value"]!.GetValue<int>());
            Assert.Equal("hi", Assert.IsType<TextMessage>(received[5].Message).Text);
            Assert.Null(received[6].Data);
            Assert.Null(received[6].Message);
        }
        finally
        {
            EventManager.Unregister(plugin);
        }
    }

    [Fact]
    public void Every_primitive_overload_carries_its_own_type()
    {
        var plugin = new FakePlugin();
        var register = new ComponentRegister(plugin, "demo");
        var handle = register.RegisterPluginEvent("typed");

        var received = new List<object?>();

        try
        {
            register.SubscribePluginEvent("demo", "typed", e => received.Add(e.Data));

            handle.Raise('x');
            handle.Raise(true);
            handle.Raise((byte)1);
            handle.Raise((sbyte)-2);
            handle.Raise((short)-3);
            handle.Raise((ushort)4);
            handle.Raise(5);
            handle.Raise(6u);
            handle.Raise(7L);
            handle.Raise(8UL);
            handle.Raise(1.5f);
            handle.Raise(2.5);
            handle.Raise(3.5m);

            Assert.Equal(13, received.Count);
            Assert.Equal('x', Assert.IsType<char>(received[0]));
            Assert.True(Assert.IsType<bool>(received[1]));
            Assert.Equal((byte)1, Assert.IsType<byte>(received[2]));
            Assert.Equal((sbyte)-2, Assert.IsType<sbyte>(received[3]));
            Assert.Equal((short)-3, Assert.IsType<short>(received[4]));
            Assert.Equal((ushort)4, Assert.IsType<ushort>(received[5]));
            Assert.Equal(5, Assert.IsType<int>(received[6]));
            Assert.Equal(6u, Assert.IsType<uint>(received[7]));
            Assert.Equal(7L, Assert.IsType<long>(received[8]));
            Assert.Equal(8UL, Assert.IsType<ulong>(received[9]));
            Assert.Equal(1.5f, Assert.IsType<float>(received[10]));
            Assert.Equal(2.5, Assert.IsType<double>(received[11]));
            Assert.Equal(3.5m, Assert.IsType<decimal>(received[12]));
        }
        finally
        {
            EventManager.Unregister(plugin);
        }
    }

    [Fact]
    public void Proxy_event_handle_collects_plugin_replies()
    {
        var owner = new object();
        var register = new ComponentRegister(new FakeProxy(), "qqproxy", "qqproxy");
        var dispatch = register.RegisterProxyEvent();

        try
        {
            EventManager.SubscribeMessage(owner, _ => [new TextMessage("pong")]);

            var replies = dispatch.Dispatch(new ProxyMessage
            {
                Id = "qq",
                Uuid = Guid.Empty,
                SenderType = MessageSenderType.User,
                Sender = "user",
                Message = new TextMessage("ping")
            });

            Assert.Equal("pong", Assert.IsType<TextMessage>(Assert.Single(replies)).Text);
        }
        finally
        {
            EventManager.Unregister(owner);
        }
    }

    [Fact]
    public async Task Send_handle_reaches_the_target_instance()
    {
        var owner = new object();
        var target = Guid.NewGuid();
        var register = new ComponentRegister(new FakePlugin(), "demo");
        var send = register.RegisterSend();

        var context = new ProxyMessage
        {
            Id = "qqproxy",
            Uuid = target,
            SenderType = MessageSenderType.Group,
            Sender = "user",
            Target = "group",
            Message = new TextMessage("hi")
        };

        ProxyMessage? received = null;

        try
        {
            EventManager.RegisterSendTarget(owner, (ctx, _) =>
            {
                received = ctx;

                return Task.CompletedTask;
            });

            await send.SendAsync(context, new TextMessage("hi"));

            Assert.Same(context, received);
        }
        finally
        {
            EventManager.Unregister(owner);
        }
    }

    [Fact]
    public void Plugin_cannot_register_a_proxy_event()
    {
        var register = new ComponentRegister(new FakePlugin(), "demo");

        Assert.Throws<InvalidOperationException>(() => register.RegisterProxyEvent());
    }

    [Fact]
    public async Task Send_handle_allows_replying_to_its_own_instance()
    {
        var uuid = Guid.NewGuid();
        var owner = new object();
        var register = new ComponentRegister(new FakeInstance(), "qqproxy", "qqproxy", uuid);
        var send = register.RegisterSend();
        var sent = 0;

        try
        {
            EventManager.RegisterSendTarget(owner, (_, _) =>
            {
                sent++;

                return Task.CompletedTask;
            });

            await send.SendAsync(new ProxyMessage
            {
                Id = "qqproxy",
                Uuid = uuid,
                SenderType = MessageSenderType.Group,
                Sender = "user",
                Target = "group",
                Message = new TextMessage("hi")
            }, new TextMessage("reply"));

            Assert.Equal(1, sent);
        }
        finally
        {
            EventManager.Unregister(owner);
        }
    }

    [Fact]
    public async Task Send_handle_rejects_its_own_proxy()
    {
        var register = new ComponentRegister(new FakeProxy(), "qqproxy", "qqproxy");
        var send = register.RegisterSend();

        var context = new ProxyMessage
        {
            Id = "qqproxy",
            Uuid = Guid.NewGuid(),
            SenderType = MessageSenderType.Group,
            Sender = "user",
            Target = "group",
            Message = new TextMessage("hi")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => send.SendAsync(context, new TextMessage("hi")));
    }

    [Fact]
    public void Wildcard_component_matches_every_source()
    {
        var plugin = new FakePlugin();
        var register = new ComponentRegister(plugin, "demo");
        var ping = register.RegisterPluginEvent("ping");
        var other = new ComponentRegister(new FakePlugin(), "alpha");
        var otherPing = other.RegisterPluginEvent("ping");

        var received = new List<string>();

        try
        {
            register.SubscribePluginEvent("*", "ping", e => received.Add(e.Name));

            ping.Raise();
            otherPing.Raise();

            Assert.Equal(["[demo]ping", "[alpha]ping"], received);
        }
        finally
        {
            EventManager.Unregister(plugin);
        }
    }

    [Fact]
    public void Wildcard_event_matches_every_event_of_the_component()
    {
        var plugin = new FakePlugin();
        var register = new ComponentRegister(plugin, "demo");
        var ping = register.RegisterPluginEvent("ping");
        var pong = register.RegisterPluginEvent("pong");

        var received = new List<string>();

        try
        {
            register.SubscribePluginEvent("demo", "*", e => received.Add(e.Name));

            ping.Raise();
            pong.Raise();

            Assert.Equal(["[demo]ping", "[demo]pong"], received);
        }
        finally
        {
            EventManager.Unregister(plugin);
        }
    }

    [Fact]
    public void Wildcard_component_and_event_matches_all_custom_events()
    {
        var plugin = new FakePlugin();
        var register = new ComponentRegister(plugin, "demo");
        var ping = register.RegisterPluginEvent("ping");
        var other = new ComponentRegister(new FakePlugin(), "alpha");
        var otherPong = other.RegisterPluginEvent("pong");

        var received = new List<string>();

        try
        {
            register.SubscribePluginEvent("*", "*", e => received.Add(e.Name));

            EventManager.HandlePlugin(new PluginEvent { Id = "demo", Type = PluginEventType.Loaded });
            ping.Raise();
            otherPong.Raise();

            Assert.Equal(["[demo]ping", "[alpha]pong"], received);
        }
        finally
        {
            EventManager.Unregister(plugin);
        }
    }

    [Fact]
    public void Wildcard_subscription_rejects_brackets_in_ids()
    {
        var register = new ComponentRegister(new FakePlugin(), "demo");

        Assert.Throws<ArgumentException>(() => register.SubscribePluginEvent("de[mo", "*", _ => { }));
        Assert.Throws<ArgumentException>(() => register.SubscribePluginEvent("*", "pi]ng", _ => { }));
    }

    [Fact]
    public void Subscribing_with_an_invalid_event_name_throws()
    {
        var register = new ComponentRegister(new FakePlugin(), "demo");

        Assert.Throws<ArgumentException>(() => register.SubscribePluginEvent("ping", _ => { }));
    }

    [Fact]
    public void Register_after_the_phase_throws()
    {
        var plugin = new FakePlugin();
        var register = new ComponentRegister(plugin, "demo");

        register.Close();

        Assert.Throws<InvalidOperationException>(() => register.SubscribeMessage(_ => []));
        Assert.Throws<InvalidOperationException>(() => register.SubscribePluginEvent("demo", "ping", _ => { }));
        Assert.Throws<InvalidOperationException>(() => register.RegisterPluginEvent("ping"));
    }

    [Fact]
    public void Fake_plugin_can_register_in_the_phase()
    {
        var plugin = new FakePlugin();
        var assembly = TestComponents.AddPlugin("demo", plugin);

        assembly.Init();
        assembly.Load();

        Assert.NotNull(plugin.Register);
        Assert.Equal(1, plugin.HookCount);
    }
}