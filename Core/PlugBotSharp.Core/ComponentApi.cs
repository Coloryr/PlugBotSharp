using Microsoft.AspNetCore.Builder;
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

    /// <summary>
    /// 按组件对象注册 http 接口，只在 RegisterEventHook 阶段由注册器调用
    /// </summary>
    /// <param name="owner">组件对象</param>
    /// <param name="call">回调</param>
    internal static void RegisterHttpPathByOwner(object owner, Func<HttpContext, Task> call)
    {
        switch (owner)
        {
            case IBasePlugin plugin when ComponentManager.FindPluginId(plugin) is { } pluginId:
                Register("api/" + pluginId, owner, call);
                break;
            case IBaseProxy proxy when ComponentManager.FindProxyId(proxy) is { } proxyId:
                Register("api/" + proxyId, owner, call);
                break;
            case IProxyInstasnce instance when InstanceManager.TryGetInstance(instance, out var proxyId, out var uuid):
                Register($"api/{proxyId}/{uuid}", owner, call);
                break;
            default:
                throw new InvalidOperationException(
                    BotLang.GetLang(BotLangKey.ComponentRegisterFail, owner.GetType().FullName));
        }
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