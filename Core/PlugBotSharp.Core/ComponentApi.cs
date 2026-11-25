using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PlugBotSharp.Core.Configs;
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

    // ------------------------------------------------------------------ 组件

    /// <summary>
    /// 全部组件：已加载的，加上磁盘上还没加载的
    /// </summary>
    /// <returns>组件列表，按类型和 id 排序</returns>
    public static List<ComponentInfo> GetComponents()
    {
        return ComponentManager.ListAll().Select(x => new ComponentInfo
        {
            Id = x.Meta.Id,
            Name = x.Meta.Name ?? "",
            Version = x.Meta.Version ?? "",
            Type = x.Meta.Type,
            Description = x.Meta.Description ?? "",
            Folder = x.Folder,
            Loaded = x.Assembly is not null,
            Enabled = x.Assembly?.State == ComponentState.Enabled,
            Blocked = ComponentManager.IsComponentBlocked(x.Meta.Id),
            State = ComponentManager.GetState(x.Meta.Id)
        }).ToList();
    }

    /// <summary>
    /// 组件的展示状态
    /// </summary>
    /// <param name="id">组件 id</param>
    /// <returns>enabled / disabled / loaded / unloaded / blocked</returns>
    public static string GetComponentState(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return ComponentManager.GetState(id);
    }

    /// <summary>
    /// 启动组件：没加载的先加载，然后启用；运行库没有启用这一步，加载完就算启动
    /// </summary>
    /// <param name="id">组件 id</param>
    /// <returns>失败时返回 false，原因记在日志里</returns>
    public static bool StartComponent(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var assembly = ComponentManager.Find(id);

        if (assembly is null)
        {
            if (!ComponentLoader.LoadComponent(id))
            {
                return false;
            }

            assembly = ComponentManager.Find(id);
        }

        if (assembly is null)
        {
            return false;
        }

        return assembly.Meta.Type == ComponentType.Lib || ComponentManager.EnableComponent(id);
    }

    /// <summary>
    /// 禁用组件：只停用，不卸载；运行库没有启用这一步，返回 false
    /// </summary>
    /// <param name="id">组件 id</param>
    /// <returns>失败时返回 false</returns>
    public static bool DisableComponent(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return ComponentManager.DisableComponent(id);
    }

    /// <summary>
    /// 重启组件：停用 → 重读配置 → 按原来的启用状态恢复
    /// </summary>
    /// <param name="id">组件 id</param>
    /// <returns>失败时返回 false</returns>
    public static bool ReloadComponent(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return ComponentManager.ReloadComponent(id);
    }

    /// <summary>
    /// 组件的配置项
    /// </summary>
    /// <param name="id">组件 id</param>
    /// <returns>配置项，组件没提供或者没加载时返回 null</returns>
    public static Dictionary<string, ConfigMap>? GetComponentConfig(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return ComponentManager.TryGetConfigMap(id, out var map) ? map : null;
    }

    /// <summary>
    /// 更新组件配置，交给组件的 UpdateMap
    /// </summary>
    /// <param name="id">组件 id</param>
    /// <param name="map">新的配置项</param>
    /// <returns>失败时返回 false</returns>
    public static bool UpdateComponentConfig(string id, Dictionary<string, ConfigMap> map)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(map);

        return ComponentManager.TryUpdateConfigMap(id, map);
    }

    // ------------------------------------------------------------------ 实例

    /// <summary>
    /// 全部实例：已加载的，加上磁盘上还没加载的
    /// </summary>
    /// <returns>实例列表，按代理和名称排序</returns>
    public static List<InstanceInfo> GetInstances()
    {
        return InstanceManager.List();
    }

    /// <summary>
    /// 启用实例，还没加载的先从磁盘加载
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    /// <returns>失败时返回 false，原因记在日志里</returns>
    public static bool EnableInstance(Guid uuid)
    {
        if (!InstanceManager.TryLoad(uuid, out var error))
        {
            BotManager.Logger?.Warn(error);

            return false;
        }

        // 磁盘上记着启用状态的实例，TryLoad 已经把它启起来了，别再启一遍
        if (InstanceManager.List().Any(x => x.Uuid == uuid && x.Enabled))
        {
            return true;
        }

        var ok = InstanceManager.TryEnable(uuid, out error);

        if (!ok)
        {
            BotManager.Logger?.Warn(error);
        }

        return ok;
    }

    /// <summary>
    /// 禁用实例，只停用，不卸载
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    /// <returns>失败时返回 false，原因记在日志里</returns>
    public static bool DisableInstance(Guid uuid)
    {
        var ok = InstanceManager.TryDisable(uuid, out var error);

        if (!ok)
        {
            BotManager.Logger?.Warn(error);
        }

        return ok;
    }

    /// <summary>
    /// 重启实例：停用 → 重读配置 → 按原来的启用状态恢复
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    /// <returns>失败时返回 false，原因记在日志里</returns>
    public static bool ReloadInstance(Guid uuid)
    {
        if (!InstanceManager.TryLoad(uuid, out var error))
        {
            BotManager.Logger?.Warn(error);

            return false;
        }

        var ok = InstanceManager.TryReload(uuid, out error);

        if (!ok)
        {
            BotManager.Logger?.Warn(error);
        }

        return ok;
    }

    /// <summary>
    /// 实例的配置项
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    /// <returns>配置项，实例没提供或者没加载时返回 null</returns>
    public static Dictionary<string, ConfigMap>? GetInstanceConfig(Guid uuid)
    {
        return InstanceManager.TryGetConfigMap(uuid, out var map) ? map : null;
    }

    /// <summary>
    /// 更新实例配置，交给实例的 UpdateMap
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    /// <param name="map">新的配置项</param>
    /// <returns>失败时返回 false</returns>
    public static bool UpdateInstanceConfig(Guid uuid, Dictionary<string, ConfigMap> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return InstanceManager.TryUpdateConfigMap(uuid, map);
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