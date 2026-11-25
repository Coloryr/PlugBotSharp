using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PlugBotSharp.Core.Http;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.Core.Plugin;
using PlugBotSharp.Core.Proxy;

namespace PlugBotSharp.Core;

/// <summary>
/// 组件可以调用的宿主接口
/// </summary>
public static class ComponentApi
{
    /// <summary>
    /// 注册 http 接口，路径为 /api/{id}
    /// </summary>
    /// <param name="plugin">插件实例，需已加载到宿主</param>
    /// <param name="call">回调，返回 Task</param>
    public static void RegisterHttpPath(IBasePlugin plugin, Func<HttpContext, Task> call)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(call);

        var id = ComponentManager.FindPluginId(plugin)
            ?? throw new InvalidOperationException(BotLang.GetLang(BotLangKey.ComponentRegisterFail, plugin.GetType().FullName));

        Register("api/" + id, plugin, call);
    }

    /// <summary>
    /// 注册 http 接口，路径为 /api/{id}
    /// </summary>
    /// <param name="proxy">代理实例，需已加载到宿主</param>
    /// <param name="call">回调，返回 Task</param>
    public static void RegisterHttpPath(IBaseProxy proxy, Func<HttpContext, Task> call)
    {
        ArgumentNullException.ThrowIfNull(proxy);
        ArgumentNullException.ThrowIfNull(call);

        var id = ComponentManager.FindProxyId(proxy)
            ?? throw new InvalidOperationException(BotLang.GetLang(BotLangKey.ComponentRegisterFail, proxy.GetType().FullName));

        Register("api/" + id, proxy, call);
    }

    /// <summary>
    /// 注册 http 接口，路径为 /api/{proxyId}/{uuid}
    /// </summary>
    /// <param name="instance">代理实例，需已创建</param>
    /// <param name="call">回调，返回 Task</param>
    public static void RegisterHttpPath(IProxyInstasnce instance, Func<HttpContext, Task> call)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(call);

        if (!InstanceManager.TryGetInstance(instance, out var proxyId, out var uuid))
        {
            throw new InvalidOperationException(BotLang.GetLang(BotLangKey.ComponentRegisterFail, instance.GetType().FullName));
        }

        Register($"api/{proxyId}/{uuid}", instance, call);
    }

    /// <summary>
    /// 取消注册 http 接口，路径为 /api/{id}
    /// </summary>
    /// <param name="plugin">插件实例</param>
    /// <returns>是否移除了注册</returns>
    public static bool UnregisterHttpPath(IBasePlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        var id = ComponentManager.FindPluginId(plugin);

        return id == null
            ? ApiRegistry.Unregister(plugin)
            : ApiRegistry.Unregister("api/" + id, plugin);
    }

    /// <summary>
    /// 取消注册 http 接口，路径为 /api/{id}
    /// </summary>
    /// <param name="proxy">代理实例</param>
    /// <returns>是否移除了注册</returns>
    public static bool UnregisterHttpPath(IBaseProxy proxy)
    {
        ArgumentNullException.ThrowIfNull(proxy);

        var id = ComponentManager.FindProxyId(proxy);

        return id == null
            ? ApiRegistry.Unregister(proxy)
            : ApiRegistry.Unregister("api/" + id, proxy);
    }

    /// <summary>
    /// 取消注册 http 接口，路径为 /api/{proxyId}/{uuid}
    /// </summary>
    /// <param name="instance">代理实例</param>
    /// <returns>是否移除了注册</returns>
    public static bool UnregisterHttpPath(IProxyInstasnce instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return InstanceManager.TryGetInstance(instance, out var proxyId, out var uuid)
            ? ApiRegistry.Unregister($"api/{proxyId}/{uuid}", instance)
            : ApiRegistry.Unregister(instance);
    }

    /// <summary>
    /// 注册消息事件，代理收到消息后调用，返回值是要回复的消息；在 RegisterEventHook 里调用
    /// </summary>
    /// <param name="owner">注册者，卸载时自动清理</param>
    /// <param name="handler">回调</param>
    public static void RegisterMessageHook(object owner, Func<ProxyMessage, List<BaseMessage>> handler)
    {
        EventManager.RegisterMessageHook(owner, handler);
    }

    /// <summary>
    /// 注册 Proxy 事件；在 RegisterEventHook 里调用
    /// </summary>
    /// <param name="owner">注册者，卸载时自动清理</param>
    /// <param name="handler">回调</param>
    public static void RegisterProxyHook(object owner, Action<ProxyEvent> handler)
    {
        EventManager.RegisterProxyHook(owner, handler);
    }

    /// <summary>
    /// 注册插件事件；在 RegisterEventHook 里调用
    /// </summary>
    /// <param name="owner">注册者，卸载时自动清理</param>
    /// <param name="handler">回调</param>
    public static void RegisterPluginHook(object owner, Action<PluginEvent> handler)
    {
        EventManager.RegisterPluginHook(owner, handler);
    }

    /// <summary>
    /// 注册发送钩子，别的组件把消息发到某个实例时走这里；在 RegisterEventHook 里调用
    /// </summary>
    /// <param name="owner">注册者，卸载时自动清理</param>
    /// <param name="handler">回调</param>
    public static void RegisterSendHook(object owner, Func<ProxyMessage, BaseMessage, Task> handler)
    {
        EventManager.RegisterSendHook(owner, handler);
    }

    /// <summary>
    /// 代理收到消息后调用，返回所有插件要回复的消息
    /// </summary>
    /// <param name="message">收到的消息</param>
    public static List<BaseMessage> DispatchMessage(ProxyMessage message)
    {
        return EventManager.HandleMessage(message);
    }

    /// <summary>
    /// 把消息交给目标实例发出去，目标实例按 context.Uuid 自己过滤
    /// </summary>
    /// <param name="context">消息上下文</param>
    /// <param name="message">要发的消息</param>
    public static Task SendAsync(ProxyMessage context, BaseMessage message)
    {
        return EventManager.HandleSendAsync(context, message);
    }

    /// <summary>
    /// 触发自定义插件事件
    /// </summary>
    /// <param name="id">来源插件 id</param>
    /// <param name="name">事件名</param>
    /// <param name="data">事件数据</param>
    public static void RaisePlugin(string id, string name, object? data = null)
    {
        EventManager.RaisePlugin(id, name, data);
    }

    /// <summary>
    /// 起一个由宿主托管的 http / websocket 服务器，组件结束时会自动关闭
    /// </summary>
    /// <param name="owner">组件的插件 / 代理 / 实例对象</param>
    /// <param name="options">监听参数</param>
    /// <param name="configure">路由配置，拿到 app 之后自己 Map</param>
    /// <returns>服务器句柄，调用 <see cref="ComponentServer.Start"/> 才会真正启动</returns>
    public static ComponentServer StartServer(object owner, ServerOptions options, Action<WebApplication> configure)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        var server = new ComponentServer(options, configure);

        ServerRegistry.Add(owner, server);

        return server;
    }

    private static void Register(string path, object owner, Func<HttpContext, Task> call)
    {
        ApiRegistry.Register(path, new ApiEntry
        {
            Owner = owner,
            Call = call
        });
    }
}