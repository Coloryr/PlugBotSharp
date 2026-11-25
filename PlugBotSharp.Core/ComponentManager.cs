using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Lib;
using PlugBotSharp.Core.Plugin;
using PlugBotSharp.Core.Proxy;

namespace PlugBotSharp.Core;

/// <summary>
/// 组件管理：加载后的启用、禁用、卸载
/// </summary>
internal static class ComponentManager
{
    internal static ConcurrentDictionary<string, PluginAssembly> Plugins = [];
    internal static ConcurrentDictionary<string, ProxyAssembly> Proxys = [];
    internal static ConcurrentDictionary<string, LibAssembly> Libs = [];

    /// <summary>
    /// 依赖不满足、被禁止启用的组件
    /// </summary>
    private static readonly HashSet<string> s_blocked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 启动：按启用记录启用插件和代理
    /// </summary>
    public static void Start()
    {
        EnableComponents();
    }

    /// <summary>
    /// 停止：卸载所有组件
    /// </summary>
    public static void Stop()
    {
        UnloadAll();
    }

    /// <summary>
    /// 已加载的组件，找不到返回 null
    /// </summary>
    /// <param name="id">组件 id</param>
    internal static BaseAssembly? Find(string id)
    {
        if (Plugins.TryGetValue(id, out var plugin))
        {
            return plugin;
        }

        if (Proxys.TryGetValue(id, out var proxy))
        {
            return proxy;
        }

        if (Libs.TryGetValue(id, out var lib))
        {
            return lib;
        }

        return null;
    }

    /// <summary>
    /// 组件是否已加载到宿主
    /// </summary>
    /// <param name="id">组件 id</param>
    internal static bool IsLoaded(string id)
    {
        return Plugins.ContainsKey(id) || Proxys.ContainsKey(id) || Libs.ContainsKey(id);
    }

    /// <summary>
    /// 组件的展示状态：blocked / unloaded / enabled / disabled / loaded
    /// </summary>
    /// <param name="id">组件 id</param>
    internal static string GetState(string id)
    {
        if (IsComponentBlocked(id))
        {
            return "blocked";
        }

        var assembly = Find(id);

        if (assembly is null)
        {
            return "unloaded";
        }

        return assembly.State switch
        {
            ComponentState.Enabled => "enabled",
            ComponentState.Disabled => "disabled",
            _ => "loaded"
        };
    }

    /// <summary>
    /// 全部组件：已加载的，加上磁盘上还没加载的
    /// </summary>
    internal static List<(BaseAssembly? Assembly, MetaObj Meta, string Folder)> ListAll()
    {
        var result = new List<(BaseAssembly? Assembly, MetaObj Meta, string Folder)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in All())
        {
            seen.Add(assembly.Meta.Id);
            result.Add((assembly, assembly.Meta, assembly.Folder));
        }

        if (Directory.Exists(BotManager.ComponentsPath))
        {
            foreach (var folder in Directory.EnumerateDirectories(BotManager.ComponentsPath))
            {
                // 安装临时目录
                if (Path.GetFileName(folder).StartsWith('.'))
                {
                    continue;
                }

                var meta = ComponentLoader.TryReadMeta(folder);

                if (meta is null || !seen.Add(meta.Id))
                {
                    continue;
                }

                result.Add((null, meta, folder));
            }
        }

        result.Sort((a, b) =>
        {
            var type = a.Meta.Type.CompareTo(b.Meta.Type);

            return type != 0 ? type : string.Compare(a.Meta.Id, b.Meta.Id, StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    /// <summary>
    /// 已加载的全部组件，顺序为 Lib、Plugin、Proxy
    /// </summary>
    internal static IEnumerable<BaseAssembly> All()
    {
        foreach (var item in Libs.Values)
        {
            yield return item;
        }

        foreach (var item in Plugins.Values)
        {
            yield return item;
        }

        foreach (var item in Proxys.Values)
        {
            yield return item;
        }
    }

    /// <summary>
    /// 根据插件实例找它的组件 id
    /// </summary>
    internal static string? FindPluginId(IBasePlugin plugin)
    {
        foreach (var pair in Plugins)
        {
            if (ReferenceEquals(pair.Value.Plugin, plugin))
            {
                return pair.Key;
            }
        }

        return null;
    }

    /// <summary>
    /// 根据代理实例找它的组件 id
    /// </summary>
    internal static string? FindProxyId(IBaseProxy proxy)
    {
        foreach (var pair in Proxys)
        {
            if (ReferenceEquals(pair.Value.Proxy, proxy))
            {
                return pair.Key;
            }
        }

        return null;
    }

    /// <summary>
    /// 组件是否因为依赖不满足被禁止启用
    /// </summary>
    public static bool IsComponentBlocked(string id)
    {
        lock (s_blocked)
        {
            return s_blocked.Contains(id);
        }
    }

    /// <summary>
    /// 标记组件依赖不满足，禁止启用
    /// </summary>
    internal static void BlockComponent(string id)
    {
        lock (s_blocked)
        {
            s_blocked.Add(id);
        }
    }

    /// <summary>
    /// 启用组件
    /// </summary>
    public static bool EnableComponent(string id)
    {
        if (IsComponentBlocked(id))
        {
            BotManager.Logger?.Warn(BotLang.GetLang(BotLangKey.ComponentBlocked, id));
            return false;
        }

        if (Plugins.TryGetValue(id, out var plugin))
        {
            return Guard(id, plugin.Enable);
        }

        if (Proxys.TryGetValue(id, out var proxy))
        {
            if (!Guard(id, proxy.Enable))
            {
                return false;
            }

            // 代理启用后，把它在磁盘上的实例重新加载起来
            InstanceManager.EnableProxyInstances(id);

            return true;
        }

        if (Libs.ContainsKey(id))
        {
            BotManager.Logger?.Warn(BotLang.GetLang(BotLangKey.ComponentNotSupported, id));
            return false;
        }

        BotManager.Logger?.Warn(BotLang.GetLang(BotLangKey.ComponentNotFound, id));
        return false;
    }

    /// <summary>
    /// 禁用组件
    /// </summary>
    public static bool DisableComponent(string id)
    {
        if (Plugins.TryGetValue(id, out var plugin))
        {
            return Guard(id, () => plugin.Disable());
        }

        if (Proxys.TryGetValue(id, out var proxy))
        {
            var ok = Guard(id, () => proxy.Disable());

            // 代理停用后它的实例也不能继续运行
            InstanceManager.StopInstances(id);

            return ok;
        }

        if (Libs.ContainsKey(id))
        {
            BotManager.Logger?.Warn(BotLang.GetLang(BotLangKey.ComponentNotSupported, id));
            return false;
        }

        BotManager.Logger?.Warn(BotLang.GetLang(BotLangKey.ComponentNotFound, id));
        return false;
    }

    /// <summary>
    /// 重载组件配置：Disable → Reload → Enable
    /// </summary>
    /// <param name="id">组件 id</param>
    /// <returns>组件不存在或者重载失败时返回 false</returns>
    internal static bool ReloadComponent(string id)
    {
        if (Plugins.TryGetValue(id, out var plugin))
        {
            var pluginEnabled = plugin.State == ComponentState.Enabled;

            if (pluginEnabled && !DisableComponent(id))
            {
                return false;
            }

            if (!Guard(id, () =>
            {
                plugin.Reload();
                return true;
            }))
            {
                return false;
            }

            return !pluginEnabled || EnableComponent(id);
        }

        if (!Proxys.TryGetValue(id, out var proxy))
        {
            BotManager.Logger?.Warn(BotLang.GetLang(BotLangKey.ComponentNotSupported, id));
            return false;
        }

        var enabled = proxy.State == ComponentState.Enabled;

        // 先停掉，重新读配置后按原来的启用状态恢复
        if (enabled && !DisableComponent(id))
        {
            return false;
        }

        if (!Guard(id, () =>
        {
            proxy.Reload();
            return true;
        }))
        {
            return false;
        }

        return !enabled || EnableComponent(id);
    }

    /// <summary>
    /// 执行组件代码，异常时记日志并当作失败
    /// </summary>
    /// <param name="id">组件 id</param>
    /// <param name="action">组件调用</param>
    private static bool Guard(string id, Func<bool> action)
    {
        try
        {
            return action();
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.ComponentActionFail, id));
            return false;
        }
    }

    /// <summary>
    /// 卸载组件，依赖它的组件会先被卸载
    /// </summary>
    public static bool UnloadComponent(string id)
    {
        if (!Plugins.ContainsKey(id) && !Proxys.ContainsKey(id) && !Libs.ContainsKey(id))
        {
            BotManager.Logger?.Warn(BotLang.GetLang(BotLangKey.ComponentNotFound, id));
            return false;
        }

        return UnloadComponent(id, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 递归卸载组件，依赖它的组件先卸载
    /// </summary>
    /// <param name="id">组件 id</param>
    /// <param name="visiting">已处理的 id，用于防止循环依赖</param>
    private static bool UnloadComponent(string id, HashSet<string> visiting)
    {
        // 循环依赖保护
        if (!visiting.Add(id))
        {
            return true;
        }

        // 先卸载依赖它的子组件
        foreach (var dependent in GetDependents(id))
        {
            BotManager.Logger?.Info(BotLang.GetLang(BotLangKey.ComponentUnloadDependent, dependent, id));
            UnloadComponent(dependent, visiting);
        }

        if (Plugins.TryRemove(id, out var plugin))
        {
            return Unload(plugin);
        }

        if (Proxys.TryRemove(id, out var proxy))
        {
            return Unload(proxy);
        }

        if (Libs.TryRemove(id, out var lib))
        {
            return Unload(lib);
        }

        return true;
    }

    /// <summary>
    /// 找出依赖指定组件的组件
    /// </summary>
    private static IEnumerable<string> GetDependents(string id)
    {
        foreach (var pair in Plugins)
        {
            if (DependsOn(pair.Value, id))
            {
                yield return pair.Key;
            }
        }

        foreach (var pair in Proxys)
        {
            if (DependsOn(pair.Value, id))
            {
                yield return pair.Key;
            }
        }

        foreach (var pair in Libs)
        {
            if (DependsOn(pair.Value, id))
            {
                yield return pair.Key;
            }
        }
    }

    /// <summary>
    /// 判断组件是否依赖指定组件
    /// </summary>
    private static bool DependsOn(BaseAssembly assembly, string id)
    {
        foreach (var dep in assembly.Meta.Deps ?? [])
        {
            if (dep.Type == DepType.Component && string.Equals(dep.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 卸载所有组件
    /// </summary>
    public static void UnloadAll()
    {
        foreach (var id in Plugins.Keys.ToArray())
        {
            UnloadComponent(id);
        }

        foreach (var id in Proxys.Keys.ToArray())
        {
            UnloadComponent(id);
        }

        foreach (var id in Libs.Keys.ToArray())
        {
            UnloadComponent(id);
        }
    }

    /// <summary>
    /// 组件配置目录 Configs/{组件id}，不存在时创建
    /// </summary>
    /// <param name="id">组件 id</param>
    internal static string GetConfigPath(string id)
    {
        var path = Path.Combine(BotManager.ConfigsPath, id);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 读取组件的配置项，交给网页端渲染
    /// </summary>
    /// <param name="id">组件 id</param>
    /// <param name="map">配置项</param>
    /// <returns>组件不存在或者不支持配置时返回 false</returns>
    internal static bool TryGetConfigMap(string id, out Dictionary<string, ConfigMap> map)
    {
        map = [];

        if (FindComponent(id) is not { } component)
        {
            return false;
        }

        try
        {
            map = component.ConfigMap();
            return true;
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.ComponentActionFail, id));
            return false;
        }
    }

    /// <summary>
    /// 保存网页端提交的配置项
    /// </summary>
    /// <param name="id">组件 id</param>
    /// <param name="map">配置项</param>
    /// <returns>组件不存在或者保存失败时返回 false</returns>
    internal static bool TryUpdateConfigMap(string id, Dictionary<string, ConfigMap> map)
    {
        if (FindComponent(id) is not { } component)
        {
            return false;
        }

        return Guard(id, () =>
        {
            component.UpdateMap(map);
            return true;
        });
    }

    /// <summary>
    /// 取组件的通用接口，插件和代理都实现了 IBaseComponent
    /// </summary>
    /// <param name="id">组件 id</param>
    private static IBaseComponent? FindComponent(string id)
    {
        if (Plugins.TryGetValue(id, out var plugin))
        {
            return plugin.Plugin;
        }

        if (Proxys.TryGetValue(id, out var proxy))
        {
            return proxy.Proxy;
        }

        return null;
    }

    /// <summary>
    /// 按顺序禁用并卸载组件
    /// </summary>
    private static bool Unload(BaseAssembly assembly)
    {
        if (assembly.State == ComponentState.Enabled)
        {
            try
            {
                switch (assembly)
                {
                    case PluginAssembly plugin:
                        plugin.Disable(false);
                        break;
                    case ProxyAssembly proxy:
                        proxy.Disable(false);
                        break;
                }
            }
            catch (Exception e)
            {
                BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.ComponentActionFail, assembly.Meta.Id));
            }
        }

        // 代理组件卸载前先停掉它的实例，否则实例会一直引用代理的程序集
        if (assembly is ProxyAssembly)
        {
            InstanceManager.StopInstances(assembly.Meta.Id);
        }

        var ok = true;

        try
        {
            ok = assembly switch
            {
                PluginAssembly plugin => plugin.Unload(),
                ProxyAssembly proxy => proxy.Unload(),
                LibAssembly lib => lib.Unload(),
                _ => true,
            };
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.ComponentActionFail, assembly.Meta.Id));
        }

        // 先标记卸载，再断开引用，最后等上下文被回收，之后组件文件才能删除
        var natives = (assembly.Context as ComponentLoadContext)?.TakeNatives() ?? [];
        var weak = PrepareUnload(assembly);
        var id = assembly.Meta.Id;

        assembly.Detach();

        WaitUnload(weak, id);

        // ALC 卸载不会释放 native 库，这里手动 Free
        foreach (var handle in natives)
        {
            try
            {
                NativeLibrary.Free(handle);
            }
            catch (Exception e)
            {
                BotManager.Logger?.Warn(e, BotLang.GetLang(BotLangKey.NativeUnloadFail, id));
            }
        }

        BotManager.Logger?.Info(BotLang.GetLang(BotLangKey.ComponentUnloaded, id));

        return ok;
    }

    /// <summary>
    /// 标记加载上下文卸载，返回弱引用
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference? PrepareUnload(BaseAssembly assembly)
    {
        var context = assembly.Context;

        if (context is null)
        {
            return null;
        }

        var weak = new WeakReference(context);
        context.Unload();

        return weak;
    }

    /// <summary>
    /// 等待加载上下文被回收
    /// </summary>
    private static void WaitUnload(WeakReference? weak, string id)
    {
        if (weak is null)
        {
            return;
        }

        for (var i = 0; i < 10 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        if (weak.IsAlive)
        {
            BotManager.Logger?.Warn(BotLang.GetLang(BotLangKey.ComponentUnloadPending, id));
        }
    }

    /// <summary>
    /// 按启用记录启用所有插件和代理
    /// </summary>
    private static void EnableComponents()
    {
        foreach (var id in Plugins.Keys)
        {
            EnableComponentIfNeeded(id);
        }

        foreach (var id in Proxys.Keys)
        {
            EnableComponentIfNeeded(id);
        }
    }

    /// <summary>
    /// 按启用记录启用组件，被 Block 或记录为禁用时跳过
    /// </summary>
    private static void EnableComponentIfNeeded(string id)
    {
        if (IsComponentBlocked(id) || !StateManager.IsComponentEnabled(id))
        {
            BotManager.Logger.Info(BotLang.GetLang(BotLangKey.ComponentDisabled, id));
            return;
        }

        if (!EnableComponent(id))
        {
            BotManager.Logger.Error(BotLang.GetLang(BotLangKey.ComponentEnableFail, id));
        }
    }
}
