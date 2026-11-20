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
    /// <param name="call">回调</param>
    public static void RegisterHttpPath(IBasePlugin plugin, Action<HttpContext> call)
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
    /// <param name="call">回调</param>
    public static void RegisterHttpPath(IBaseProxy proxy, Action<HttpContext> call)
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
    /// <param name="call">回调</param>
    public static void RegisterHttpPath(IProxyInstasnce instance, Action<HttpContext> call)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(call);

        if (!InstanceManager.TryGetInstance(instance, out var proxyId, out var uuid))
        {
            throw new InvalidOperationException(BotLang.GetLang(BotLangKey.ComponentRegisterFail, instance.GetType().FullName));
        }

        Register($"api/{proxyId}/{uuid}", instance, call);
    }

    private static void Register(string path, object owner, Action<HttpContext> call)
    {
        ApiRegistry.Register(path, new ApiEntry
        {
            Owner = owner,
            Call = call
        });
    }
}
