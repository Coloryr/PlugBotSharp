using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Lib;
using PlugBotSharp.Core.Nuget;
using PlugBotSharp.Core.Plugin;
using PlugBotSharp.Core.Proxy;

namespace PlugBotSharp.Core;

/// <summary>
/// 组件加载
/// </summary>
internal static class ComponentLoader
{
    /// <summary>
    /// 清单文件名
    /// </summary>
    private const string ManifestName = "Meta.json";

    /// <summary>
    /// 加载目录下的所有组件，目录结构：{ComponentsPath}/{id}/Meta.json + dll
    /// </summary>
    public static void LoadAll(string root)
    {
        if (!Directory.Exists(root))
        {
            BotManager.Logger.Warn(BotLang.GetLang(BotLangKey.ComponentDirMissing, root));
            return;
        }

        var items = new List<(string Folder, MetaObj Meta)>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in Directory.EnumerateDirectories(root))
        {
            // 跳过安装临时目录
            if (Path.GetFileName(folder).StartsWith('.'))
            {
                continue;
            }

            try
            {
                var meta = ReadMeta(folder);

                if (meta is null)
                {
                    continue;
                }

                if (!ids.Add(meta.Id))
                {
                    BotManager.Logger.Warn(BotLang.GetLang(BotLangKey.ComponentDuplicate, meta.Id, folder));
                    continue;
                }

                items.Add((folder, meta));
            }
            catch (Exception e)
            {
                BotManager.Logger.Error(e, BotLang.GetLang(BotLangKey.ComponentLoadFail, folder));
            }
        }

        // 加载前先把所有组件的 NuGet 依赖补全
        InstallNugets(items);

        // 先把被依赖的组件加载起来
        foreach (var (folder, meta) in SortByDeps(items))
        {
            try
            {
                Load(folder, meta);
            }
            catch (Exception e)
            {
                BotManager.Logger.Error(e, BotLang.GetLang(BotLangKey.ComponentLoadFail, folder));
            }
        }

        // 最后校验组件依赖
        CheckDependencies();
    }

    /// <summary>
    /// 补全所有组件声明的 NuGet 依赖
    /// </summary>
    private static void InstallNugets(List<(string Folder, MetaObj Meta)> items)
    {
        var deps = new List<(string Id, string Version)>();
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, meta) in items)
        {
            foreach (var dep in meta.Deps ?? [])
            {
                if (dep.Type == DepType.Nuget && installed.Add(dep.Id + "/" + dep.Version))
                {
                    deps.Add((dep.Id, dep.Version));
                }
            }
        }

        if (deps.Count == 0)
        {
            return;
        }

        var results = new bool[deps.Count];

        Parallel.For(0, deps.Count, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(4, Environment.ProcessorCount),
        }, i =>
        {
            results[i] = NugetManager.Install(deps[i].Id, deps[i].Version);
        });

        var failed = new List<string>();

        for (var i = 0; i < deps.Count; i++)
        {
            if (!results[i])
            {
                failed.Add(deps[i].Id + " " + deps[i].Version);
            }
        }

        if (failed.Count > 0)
        {
            BotManager.Logger.Error(BotLang.GetLang(BotLangKey.NugetFailed, string.Join(", ", failed)));
        }

        BotManager.Logger.Info(BotLang.GetLang(BotLangKey.NugetCompleted, deps.Count));
    }

    /// <summary>
    /// 读取组件的 Meta.json
    /// </summary>
    private static MetaObj? ReadMeta(string folder)
    {
        var manifest = Path.Combine(folder, ManifestName);

        if (!File.Exists(manifest))
        {
            BotManager.Logger.Warn(BotLang.GetLang(BotLangKey.ComponentManifestMissing, ManifestName, folder));
            return null;
        }

        var meta = JsonUtils.ToObj(File.ReadAllText(manifest), SourceGenerationContext.Default.MetaObj);

        if (meta is null || string.IsNullOrWhiteSpace(meta.Id))
        {
            BotManager.Logger.Warn(BotLang.GetLang(BotLangKey.ComponentManifestInvalid, ManifestName, folder));
            return null;
        }

        return meta;
    }

    /// <summary>
    /// 按依赖排序，被依赖的组件排前面
    /// </summary>
    private static List<(string Folder, MetaObj Meta)> SortByDeps(List<(string Folder, MetaObj Meta)> items)
    {
        var map = new Dictionary<string, (string Folder, MetaObj Meta)>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            map[item.Meta.Id] = item;
        }

        var result = new List<(string Folder, MetaObj Meta)>(items.Count);
        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit((string Folder, MetaObj Meta) item)
        {
            var id = item.Meta.Id;

            if (done.Contains(id) || !visiting.Add(id))
            {
                return;
            }

            foreach (var dep in item.Meta.Deps ?? [])
            {
                if (dep.Type == DepType.Component && map.TryGetValue(dep.Id, out var target))
                {
                    Visit(target);
                }
            }

            visiting.Remove(id);
            done.Add(id);
            result.Add(item);
        }

        foreach (var item in items)
        {
            Visit(item);
        }

        return result;
    }

    /// <summary>
    /// 加载单个组件
    /// </summary>
    /// <param name="folder">组件目录</param>
    /// <param name="meta">组件元数据</param>
    /// <returns>是否走到加载流程</returns>
    private static bool Load(string folder, MetaObj meta)
    {
        if (ComponentManager.IsLoaded(meta.Id))
        {
            BotManager.Logger.Warn(BotLang.GetLang(BotLangKey.ComponentDuplicate, meta.Id, folder));
            return false;
        }

        StateManager.EnsureComponentState(meta.Id);

        // NuGet 包没准备好就不加载，组件依赖统一放到 LoadAll 末尾校验
        if (!EnsureDependencies(meta))
        {
            return false;
        }

        BotManager.Logger.Info(BotLang.GetLang(BotLangKey.ComponentLoading, meta.Id, meta.Type, folder));

        switch (meta.Type)
        {
            case ComponentType.Proxy:
                LoadProxy(meta, folder);
                break;
            case ComponentType.Lib:
                LoadLib(meta, folder);
                break;
            default:
                LoadPlugin(meta, folder);
                break;
        }

        return true;
    }

    /// <summary>
    /// 按组件 id 从组件目录加载单个组件，供管理面板使用
    /// </summary>
    /// <param name="id">组件 id</param>
    /// <returns>组件是否已加载到宿主</returns>
    internal static bool LoadComponent(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        // 已经加载过就直接返回
        if (ComponentManager.Find(id) is not null)
        {
            return true;
        }

        var folder = Path.Combine(BotManager.ComponentsPath, id);

        if (!Directory.Exists(folder))
        {
            BotManager.Logger.Warn(BotLang.GetLang(BotLangKey.ComponentDirMissing, folder));
            return false;
        }

        var meta = TryReadMeta(folder);

        if (meta is null)
        {
            return false;
        }

        Load(folder, meta);

        if (ComponentManager.Find(meta.Id) is not { } assembly)
        {
            return false;
        }

        // 依赖不满足的组件不允许留在宿主里
        if (!CheckComponentDependencies(assembly))
        {
            ComponentManager.BlockComponent(assembly.Meta.Id);
            StateManager.SetComponentEnabled(assembly.Meta.Id, false);
            ComponentManager.UnloadComponent(assembly.Meta.Id);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 读取组件目录下的清单，失败返回 null（不写日志，供面板扫描用）
    /// </summary>
    /// <param name="folder">组件目录</param>
    /// <returns>组件元数据</returns>
    internal static MetaObj? TryReadMeta(string folder)
    {
        var manifest = Path.Combine(folder, ManifestName);

        if (!File.Exists(manifest))
        {
            return null;
        }

        try
        {
            var meta = JsonUtils.ToObj(File.ReadAllText(manifest), SourceGenerationContext.Default.MetaObj);

            return string.IsNullOrWhiteSpace(meta?.Id) ? null : meta;
        }
        catch (Exception e)
        {
            BotManager.Logger.Warn(e, BotLang.GetLang(BotLangKey.ComponentManifestInvalid, ManifestName, folder));
            return null;
        }
    }

    /// <summary>
    /// 准备 NuGet 依赖
    /// </summary>
    private static bool EnsureDependencies(MetaObj meta)
    {
        foreach (var dep in meta.Deps ?? [])
        {
            if (dep.Type == DepType.Nuget && !NugetManager.Install(dep.Id, dep.Version))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// LoadAll 的最后一步：校验组件依赖，不满足的禁用并卸载
    /// </summary>
    private static void CheckDependencies()
    {
        var assemblies = new List<BaseAssembly>();

        assemblies.AddRange(ComponentManager.Plugins.Values);
        assemblies.AddRange(ComponentManager.Proxys.Values);
        assemblies.AddRange(ComponentManager.Libs.Values);

        foreach (var assembly in assemblies)
        {
            if (CheckComponentDependencies(assembly))
            {
                continue;
            }

            var id = assembly.Meta.Id;

            ComponentManager.BlockComponent(id);
            StateManager.SetComponentEnabled(id, false);
            ComponentManager.UnloadComponent(id);
            BotManager.Logger.Warn(BotLang.GetLang(BotLangKey.ComponentBlocked, id));
        }
    }

    /// <summary>
    /// 单个组件的依赖校验
    /// </summary>
    /// <param name="assembly">组件</param>
    /// <returns>依赖是否满足</returns>
    internal static bool CheckComponentDependencies(BaseAssembly assembly)
    {
        foreach (var dep in assembly.Meta.Deps ?? [])
        {
            if (dep.Type != DepType.Component)
            {
                continue;
            }

            if (!TryGetComponentVersion(dep.Id, out var version))
            {
                BotManager.Logger.Error(BotLang.GetLang(BotLangKey.ComponentDepMissing, assembly.Meta.Id, dep.Id));
                return false;
            }

            if (!VersionUtil.Satisfies(version, dep.Version))
            {
                BotManager.Logger.Error(BotLang.GetLang(BotLangKey.ComponentDepVersion, assembly.Meta.Id, dep.Id, dep.Version, version));
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 已加载组件的版本
    /// </summary>
    private static bool TryGetComponentVersion(string id, out string version)
    {
        if (ComponentManager.Libs.TryGetValue(id, out var lib))
        {
            version = lib.Meta.Version;
            return true;
        }

        if (ComponentManager.Plugins.TryGetValue(id, out var plugin))
        {
            version = plugin.Meta.Version;
            return true;
        }

        if (ComponentManager.Proxys.TryGetValue(id, out var proxy))
        {
            version = proxy.Meta.Version;
            return true;
        }

        version = "";
        return false;
    }

    /// <summary>
    /// 加载插件组件
    /// </summary>
    private static void LoadPlugin(MetaObj meta, string folder)
    {
        var context = new ComponentLoadContext(folder);
        var type = FindType(meta, folder, context, typeof(IBasePlugin), out var error);

        if (type is null)
        {
            context.Unload();
            BotManager.Logger.Error(BotLang.GetLang(BotLangKey.ComponentLoadFail, meta.Id + " - " + error));
            return;
        }

        if (Activator.CreateInstance(type) is not IBasePlugin plugin)
        {
            context.Unload();
            BotManager.Logger.Error(BotLang.GetLang(BotLangKey.ComponentInstanceFail, meta.Id, type.FullName));
            return;
        }

        var assembly = new PluginAssembly(plugin)
        {
            Meta = meta,
            Folder = folder,
            Context = context
        };

        // 先放进字典，实例注册 http 路径时要靠它解析出 uuid
        ComponentManager.Plugins[meta.Id] = assembly;

        try
        {
            if (!assembly.Init() || !assembly.Load())
            {
                ComponentManager.Plugins.TryRemove(meta.Id, out _);
                context.Unload();
                BotManager.Logger.Error(BotLang.GetLang(BotLangKey.ComponentInitFail, meta.Id));
            }
        }
        catch (Exception e)
        {
            // 组件代码抛异常时不能留在字典里
            ComponentManager.Plugins.TryRemove(meta.Id, out _);
            context.Unload();
            BotManager.Logger.Error(e, BotLang.GetLang(BotLangKey.ComponentInitFail, meta.Id));
        }

        // 启用交给 ComponentManager
    }

    /// <summary>
    /// 加载运行库组件，只调用 Init + Load
    /// </summary>
    private static void LoadLib(MetaObj meta, string folder)
    {
        var context = new ComponentLoadContext(folder);
        var type = FindType(meta, folder, context, typeof(IBaseLib), out var error);

        if (type is null)
        {
            context.Unload();
            BotManager.Logger.Error(BotLang.GetLang(BotLangKey.ComponentLoadFail, meta.Id + " - " + error));
            return;
        }

        if (Activator.CreateInstance(type) is not IBaseLib lib)
        {
            context.Unload();
            BotManager.Logger.Error(BotLang.GetLang(BotLangKey.ComponentInstanceFail, meta.Id, type.FullName));
            return;
        }

        var assembly = new LibAssembly(lib)
        {
            Meta = meta,
            Folder = folder,
            Context = context
        };

        ComponentManager.Libs[meta.Id] = assembly;

        try
        {
            // 运行库：只 Init + Load，不调用 Enable
            if (!assembly.Init() || !assembly.Load())
            {
                ComponentManager.Libs.TryRemove(meta.Id, out _);
                context.Unload();
                BotManager.Logger.Error(BotLang.GetLang(BotLangKey.ComponentInitFail, meta.Id));
            }
        }
        catch (Exception e)
        {
            ComponentManager.Libs.TryRemove(meta.Id, out _);
            context.Unload();
            BotManager.Logger.Error(e, BotLang.GetLang(BotLangKey.ComponentInitFail, meta.Id));
        }
    }

    /// <summary>
    /// 加载代理组件
    /// </summary>
    private static void LoadProxy(MetaObj meta, string folder)
    {
        var context = new ComponentLoadContext(folder);
        var type = FindType(meta, folder, context, typeof(IBaseProxy), out var error);

        if (type is null)
        {
            context.Unload();
            BotManager.Logger.Error(BotLang.GetLang(BotLangKey.ComponentLoadFail, meta.Id + " - " + error));
            return;
        }

        if (Activator.CreateInstance(type) is not IBaseProxy proxy)
        {
            context.Unload();
            BotManager.Logger.Error(BotLang.GetLang(BotLangKey.ComponentInstanceFail, meta.Id, type.FullName));
            return;
        }

        var assembly = new ProxyAssembly(proxy)
        {
            Meta = meta,
            Folder = folder,
            Context = context
        };

        ComponentManager.Proxys[meta.Id] = assembly;

        try
        {
            if (!assembly.Init() || !assembly.Load())
            {
                ComponentManager.Proxys.TryRemove(meta.Id, out _);
                context.Unload();
                BotManager.Logger.Error(BotLang.GetLang(BotLangKey.ComponentInitFail, meta.Id));
            }
        }
        catch (Exception e)
        {
            ComponentManager.Proxys.TryRemove(meta.Id, out _);
            context.Unload();
            BotManager.Logger.Error(e, BotLang.GetLang(BotLangKey.ComponentInitFail, meta.Id));
        }

        // 启用交给 ComponentManager
    }

    /// <summary>
    /// 加载 Meta.json 里声明的 dll，并找到指定接口的实现类型
    /// </summary>
    private static Type? FindType(MetaObj meta, string folder, AssemblyLoadContext context, Type interfaceType, out string? error)
    {
        error = null;

        if (meta.Dlls is null || meta.Dlls.Count == 0)
        {
            error = BotLang.GetLang(BotLangKey.ComponentDllsEmpty, ManifestName);
            return null;
        }

        var types = new List<Type>();

        foreach (var dll in meta.Dlls)
        {
            var path = Path.Combine(folder, dll);

            if (!File.Exists(path))
            {
                error = BotLang.GetLang(BotLangKey.ComponentDllMissing, path);
                return null;
            }

            types.AddRange(context.LoadFromAssemblyPath(path).GetTypes());
        }

        var type = types.FirstOrDefault(t =>
            interfaceType.IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });

        if (type is null)
        {
            error = BotLang.GetLang(BotLangKey.ComponentTypeMissing, interfaceType.Name);
        }

        return type;
    }

    /// <summary>
    /// 从已加载的组件里解析程序集，找不到返回 null
    /// </summary>
    internal static Assembly? ResolveFromComponents(AssemblyName name)
    {
        if (name.Name is null)
        {
            return null;
        }

        foreach (var component in EnumerateComponents())
        {
            if (component.Context is not { } context)
            {
                continue;
            }

            var loaded = context.Assemblies.FirstOrDefault(a =>
                string.Equals(a.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase));

            if (loaded is not null)
            {
                return loaded;
            }

            var path = Path.Combine(component.Folder, name.Name + ".dll");

            if (File.Exists(path))
            {
                return context.LoadFromAssemblyName(name);
            }
        }

        return null;
    }

    /// <summary>
    /// 从其他组件目录里找 native 库
    /// </summary>
    /// <param name="fileName">文件名，例如 libSkiaSharp.dll</param>
    /// <param name="path">找到的路径</param>
    internal static bool TryResolveNative(string fileName, out string path)
    {
        foreach (var component in EnumerateComponents())
        {
            var direct = Path.Combine(component.Folder, fileName);

            if (File.Exists(direct))
            {
                path = direct;
                return true;
            }

            var runtimes = Path.Combine(component.Folder, "runtimes");

            if (!Directory.Exists(runtimes))
            {
                continue;
            }

            var match = Directory.EnumerateFiles(runtimes, fileName, SearchOption.AllDirectories).FirstOrDefault();

            if (match is not null)
            {
                path = match;
                return true;
            }
        }

        path = "";
        return false;
    }

    /// <summary>
    /// native 库的候选文件名
    /// </summary>
    internal static IEnumerable<string> NativeCandidates(string name)
    {
        if (Path.HasExtension(name))
        {
            yield return name;
            yield break;
        }

        yield return name;
        yield return name + ".dll";
        yield return name + ".so";
        yield return "lib" + name + ".so";
        yield return name + ".dylib";
        yield return "lib" + name + ".dylib";
    }

    /// <summary>
    /// 已加载的组件
    /// </summary>
    private static IEnumerable<BaseAssembly> EnumerateComponents()
    {
        foreach (var item in ComponentManager.Libs.Values)
        {
            yield return item;
        }

        foreach (var item in ComponentManager.Plugins.Values)
        {
            yield return item;
        }

        foreach (var item in ComponentManager.Proxys.Values)
        {
            yield return item;
        }
    }

}

/// <summary>
/// 每个组件独立的加载上下文，卸载组件时可以回收
/// </summary>
internal sealed class ComponentLoadContext(string folder) : AssemblyLoadContext(isCollectible: true)
{
    private static readonly string s_coreName = typeof(BotManager).Assembly.GetName().Name!;

    /// <summary>
    /// 已加载的 native 库路径 -> 句柄
    /// </summary>
    private readonly Dictionary<string, IntPtr> s_natives = [];

    protected override Assembly? Load(AssemblyName name)
    {
        // Core 由宿主提供，避免类型重复
        if (name.Name == s_coreName)
        {
            return null;
        }

        if (name.Name is null)
        {
            return null;
        }

        // 先找自己目录
        var path = Path.Combine(folder, name.Name + ".dll");

        if (File.Exists(path))
        {
            return LoadFromAssemblyPath(path);
        }

        // 再找其他组件（Lib / Plugin / Proxy）
        var component = ComponentLoader.ResolveFromComponents(name);

        if (component is not null)
        {
            return component;
        }

        // 最后找 NuGet 包
        return NugetManager.Load(name);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        foreach (var candidate in ComponentLoader.NativeCandidates(unmanagedDllName))
        {
            // 自己目录
            var local = Path.Combine(folder, candidate);

            if (File.Exists(local))
            {
                return LoadNative(local);
            }

            // 自己目录下的 runtimes/{rid}/native
            var runtimes = Path.Combine(folder, "runtimes");

            if (Directory.Exists(runtimes))
            {
                var match = Directory.EnumerateFiles(runtimes, candidate, SearchOption.AllDirectories).FirstOrDefault();

                if (match is not null)
                {
                    return LoadNative(match);
                }
            }

            // NuGet 包里的 native 库
            if (NugetManager.TryResolveNative(candidate, out var nugetPath))
            {
                return LoadNative(nugetPath);
            }

            // 其他组件目录
            if (ComponentLoader.TryResolveNative(candidate, out var sharedPath))
            {
                return LoadNative(sharedPath);
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// 加载 native 库并记录下来，卸载组件时手动释放
    /// </summary>
    private IntPtr LoadNative(string path)
    {
        lock (s_natives)
        {
            if (s_natives.TryGetValue(path, out var handle))
            {
                return handle;
            }

            handle = NativeLibrary.Load(path);
            s_natives[path] = handle;

            return handle;
        }
    }

    /// <summary>
    /// 取走这个组件加载的 native 库句柄
    /// </summary>
    internal IntPtr[] TakeNatives()
    {
        lock (s_natives)
        {
            var handles = s_natives.Values.ToArray();
            s_natives.Clear();

            return handles;
        }
    }
}
