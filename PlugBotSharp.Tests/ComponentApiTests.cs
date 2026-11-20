using Microsoft.AspNetCore.Http;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;

namespace PlugBotSharp.Tests;

public class ComponentApiTests : ComponentTestBase
{
    [Fact]
    public void RegisterHttpPath_for_plugin_uses_component_id()
    {
        var plugin = new FakePlugin();
        TestComponents.AddPlugin("demo", plugin);

        Action<HttpContext> handler = _ => { };

        ComponentApi.RegisterHttpPath(plugin, handler);

        Assert.True(ApiRegistry.TryGet("api/demo", out var call));
        Assert.Same(handler, call);

        ApiRegistry.Unregister(plugin);

        Assert.False(ApiRegistry.TryGet("api/demo", out _));
    }

    [Fact]
    public void RegisterHttpPath_for_proxy_uses_component_id()
    {
        var proxy = new FakeProxy();
        TestComponents.AddProxy("qq", proxy);

        Action<HttpContext> handler = _ => { };

        ComponentApi.RegisterHttpPath(proxy, handler);

        Assert.True(ApiRegistry.TryGet("api/qq", out var call));
        Assert.Same(handler, call);

        ApiRegistry.Unregister(proxy);

        Assert.False(ApiRegistry.TryGet("api/qq", out _));
    }

    [Fact]
    public void RegisterHttpPath_for_instance_throws_when_not_created()
    {
        Assert.Throws<InvalidOperationException>(() => ComponentApi.RegisterHttpPath(new FakeInstance(), _ => { }));
    }

    [Fact]
    public void RegisterHttpPath_throws_when_plugin_not_loaded()
    {
        var plugin = new FakePlugin();

        Assert.Throws<InvalidOperationException>(() => ComponentApi.RegisterHttpPath(plugin, _ => { }));
    }
}
