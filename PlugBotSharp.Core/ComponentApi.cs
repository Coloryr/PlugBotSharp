using Microsoft.AspNetCore.Http;
using PlugBotSharp.Core.Http;
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

    private static void Register(string path, object owner, Func<HttpContext, Task> call)
    {
        ApiRegistry.Register(path, new ApiEntry
        {
            Owner = owner,
            Call = call
        });
    }
}