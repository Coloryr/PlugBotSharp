using Microsoft.AspNetCore.Builder;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core;

/// <summary>
/// 组件可以调用的宿主接口：自己不做实现，交给宿主注入的 <see cref="IApiHost"/>
/// </summary>
public static class ComponentApi
{
    /// <summary>
    /// 取消注册 http 接口，路径为 /api/{id}
    /// </summary>
    /// <param name="plugin">插件实例</param>
    public static bool UnregisterHttpPath(IBasePlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        return ApiHost.Host.UnregisterHttpPath(plugin);
    }

    /// <summary>
    /// 取消注册 http 接口，路径为 /api/{id}
    /// </summary>
    /// <param name="proxy">代理实例</param>
    public static bool UnregisterHttpPath(IBaseProxy proxy)
    {
        ArgumentNullException.ThrowIfNull(proxy);

        return ApiHost.Host.UnregisterHttpPath(proxy);
    }

    /// <summary>
    /// 取消注册 http 接口，路径为 /api/{proxyId}/{uuid}
    /// </summary>
    /// <param name="instance">代理实例</param>
    public static bool UnregisterHttpPath(IProxyInstasnce instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return ApiHost.Host.UnregisterHttpPath(instance);
    }

    /// <summary>
    /// 起一个由宿主托管的 http / websocket 服务器，组件结束时会自动关闭
    /// </summary>
    /// <param name="owner">组件的插件 / 代理 / 实例对象</param>
    /// <param name="options">监听参数</param>
    /// <param name="configure">路由配置，拿到 app 之后自己 Map</param>
    public static ComponentServer StartServer(object owner, ServerOptions options, Action<WebApplication> configure)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        var server = new ComponentServer(options, configure);

        ApiHost.Host.AddServer(owner, server);

        return server;
    }

    /// <summary>
    /// 宿主当前的语言，组件做多语言时读这个
    /// </summary>
    public static Lang Lang => ApiHost.Host.Lang;

    // ------------------------------------------------------------------ 组件

    /// <summary>
    /// 全部组件：已加载的，加上磁盘上还没加载的
    /// </summary>
    public static List<ComponentInfo> GetComponents()
    {
        return ApiHost.Host.GetComponents();
    }

    /// <summary>
    /// 组件的展示状态
    /// </summary>
    /// <param name="id">组件 id</param>
    public static string GetComponentState(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return ApiHost.Host.GetComponentState(id);
    }

    /// <summary>
    /// 启动组件：没加载的先加载，然后启用
    /// </summary>
    /// <param name="id">组件 id</param>
    public static bool StartComponent(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return ApiHost.Host.StartComponent(id);
    }

    /// <summary>
    /// 禁用组件，只停用不卸载
    /// </summary>
    /// <param name="id">组件 id</param>
    public static bool DisableComponent(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return ApiHost.Host.DisableComponent(id);
    }

    /// <summary>
    /// 重启组件：停用 → 重读配置 → 恢复原来的状态
    /// </summary>
    /// <param name="id">组件 id</param>
    public static bool ReloadComponent(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return ApiHost.Host.ReloadComponent(id);
    }

    /// <summary>
    /// 组件的配置项，没有配置时返回 null
    /// </summary>
    /// <param name="id">组件 id</param>
    public static Dictionary<string, ConfigMap>? GetComponentConfig(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return ApiHost.Host.GetComponentConfig(id);
    }

    /// <summary>
    /// 更新组件配置，交给组件的 UpdateMap
    /// </summary>
    /// <param name="id">组件 id</param>
    /// <param name="map">新的配置项</param>
    public static bool UpdateComponentConfig(string id, Dictionary<string, ConfigMap> map)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(map);

        return ApiHost.Host.UpdateComponentConfig(id, map);
    }

    // ------------------------------------------------------------------ 实例

    /// <summary>
    /// 全部实例：已加载的，加上磁盘上还没加载的
    /// </summary>
    public static List<InstanceInfo> GetInstances()
    {
        return ApiHost.Host.GetInstances();
    }

    /// <summary>
    /// 启用实例，还没加载的先从磁盘加载
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    public static bool EnableInstance(Guid uuid)
    {
        return ApiHost.Host.EnableInstance(uuid);
    }

    /// <summary>
    /// 禁用实例，只停用不卸载
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    public static bool DisableInstance(Guid uuid)
    {
        return ApiHost.Host.DisableInstance(uuid);
    }

    /// <summary>
    /// 重启实例：停用 → 重读配置 → 恢复原来的状态
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    public static bool ReloadInstance(Guid uuid)
    {
        return ApiHost.Host.ReloadInstance(uuid);
    }

    /// <summary>
    /// 实例的配置项，没有配置时返回 null
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    public static Dictionary<string, ConfigMap>? GetInstanceConfig(Guid uuid)
    {
        return ApiHost.Host.GetInstanceConfig(uuid);
    }

    /// <summary>
    /// 更新实例配置，交给实例的 UpdateMap
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    /// <param name="map">新的配置项</param>
    public static bool UpdateInstanceConfig(Guid uuid, Dictionary<string, ConfigMap> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return ApiHost.Host.UpdateInstanceConfig(uuid, map);
    }
}
