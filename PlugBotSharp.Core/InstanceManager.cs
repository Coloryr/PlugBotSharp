using System.Collections.Concurrent;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Http;
using PlugBotSharp.Core.Proxy;

namespace PlugBotSharp.Core;

/// <summary>
/// 实例信息，供管理面板展示
/// </summary>
/// <param name="Uuid">实例 uuid</param>
/// <param name="Proxy">所属代理组件 id</param>
/// <param name="Name">实例名称</param>
/// <param name="Folder">实例目录</param>
/// <param name="Loaded">是否已加载到宿主</param>
/// <param name="Enabled">是否已启用</param>
internal sealed record InstanceInfo(Guid Uuid, string Proxy, string Name, string Folder, bool Loaded, bool Enabled);

/// <summary>
/// 实例管理：创建实例、启用实例、驱动 Tick
/// </summary>
internal static class InstanceManager
{
    private const string InstanceManifest = "Instance.json";
    private const int TickInterval = 1000;

    private sealed class InstanceEntry
    {
        public required string ProxyId { get; init; }
        public required string Name { get; init; }
        public required string Folder { get; init; }
        public required Guid Uuid { get; init; }
        public required IProxyInstasnce Instance { get; init; }
        public bool Enabled { get; set; }
    }

    private static readonly ConcurrentDictionary<Guid, InstanceEntry> s_instances = new();

    /// <summary>
    /// 保护实例的创建、加载和移除，避免和 Tick 冲突
    /// </summary>
    private static readonly Lock s_lock = new();

    private static Timer? s_timer;

    /// <summary>
    /// 启动：创建实例 → 启用实例 → 开始 Tick
    /// </summary>
    public static void Start()
    {
        CreateInstances();
        EnableInstances();
        StartTick();
    }

    /// <summary>
    /// 停止：停止 Tick → 停止实例
    /// </summary>
    public static void Stop()
    {
        StopTick();
        StopInstances();
    }

    /// <summary>
    /// 列出全部实例，包含已加载和只在磁盘上的
    /// </summary>
    internal static List<InstanceInfo> List()
    {
        var result = new List<InstanceInfo>();
        var seen = new HashSet<Guid>();

        foreach (var entry in s_instances.Values)
        {
            seen.Add(entry.Uuid);
            result.Add(new InstanceInfo(entry.Uuid, entry.ProxyId, entry.Name, entry.Folder, true, entry.Enabled));
        }

        foreach (var item in EnumerateManifests())
        {
            if (!seen.Add(item.Uuid))
            {
                continue;
            }

            // 没有加载的实例不算启用
            result.Add(new InstanceInfo(item.Uuid, item.Obj.Proxy, item.Obj.Name ?? "", item.Folder, false, false));
        }

        return result
            .OrderBy(x => x.Proxy, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 根据实例对象找它所属的代理和 uuid
    /// </summary>
    internal static bool TryGetInstance(IProxyInstasnce instance, out string proxyId, out string uuid)
    {
        foreach (var entry in s_instances.Values)
        {
            if (ReferenceEquals(entry.Instance, instance))
            {
                proxyId = entry.ProxyId;
                uuid = entry.Uuid.ToString();
                return true;
            }
        }

        proxyId = "";
        uuid = "";
        return false;
    }

    /// <summary>
    /// 读取实例的配置项，交给网页端渲染
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    /// <param name="map">配置项</param>
    /// <returns>实例不存在或者不支持配置时返回 false</returns>
    internal static bool TryGetConfigMap(Guid uuid, out Dictionary<string, ConfigMap> map)
    {
        map = [];

        if (!s_instances.TryGetValue(uuid, out var entry))
        {
            return false;
        }

        try
        {
            map = entry.Instance.ConfigMap();
            return true;
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.InstanceActionFail, uuid));
            return false;
        }
    }

    /// <summary>
    /// 保存网页端提交的实例配置项
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    /// <param name="map">配置项</param>
    /// <returns>实例不存在或者保存失败时返回 false</returns>
    internal static bool TryUpdateConfigMap(Guid uuid, Dictionary<string, ConfigMap> map)
    {
        if (!s_instances.TryGetValue(uuid, out var entry))
        {
            return false;
        }

        lock (s_lock)
        {
            return Call(uuid.ToString(), () =>
            {
                entry.Instance.UpdateMap(map);
                return true;
            });
        }
    }

    /// <summary>
    /// 创建并加载实例，目录为 Instances/{uuid}
    /// </summary>
    /// <param name="proxyId">代理组件 id</param>
    /// <param name="name">实例名称</param>
    /// <param name="uuid">创建出来的实例 uuid</param>
    /// <param name="loaded">实例是否加载成功，失败时目录会保留，填好配置后可以再加载</param>
    /// <param name="error">失败原因</param>
    internal static bool TryCreate(string proxyId, string name, out Guid uuid, out bool loaded, out string error)
    {
        uuid = Guid.Empty;
        loaded = false;
        error = "";

        if (string.IsNullOrWhiteSpace(proxyId))
        {
            error = BotLang.GetLang(BotLangKey.ComponentIdInvalid, proxyId ?? "");
            return false;
        }

        if (!ComponentManager.Proxys.TryGetValue(proxyId, out var proxy))
        {
            error = BotLang.GetLang(BotLangKey.ComponentNotFound, proxyId);
            return false;
        }

        if (proxy.State != ComponentState.Enabled)
        {
            error = BotLang.GetLang(BotLangKey.InstanceProxyNotReady, proxyId);
            return false;
        }

        lock (s_lock)
        {
            uuid = Guid.NewGuid();

            var folder = Path.Combine(BotManager.InstancesPath, uuid.ToString());

            try
            {
                Directory.CreateDirectory(folder);

                var obj = new InstanceObj
                {
                    Proxy = proxyId,
                    Id = uuid.ToString(),
                    Name = name ?? "",
                    Uuid = uuid
                };

                File.WriteAllText(Path.Combine(folder, InstanceManifest),
                    JsonUtils.ToString(obj, SourceGenerationContext.Default.InstanceObj));
            }
            catch (Exception e)
            {
                error = e.Message;
                TryDeleteFolder(folder);
                return false;
            }

            StateManager.SetInstanceEnabled(uuid.ToString(), true);

            // 加载失败时保留目录，用户补完配置后可以在面板里再加载
            if (!AddInstance(folder, proxyId, uuid, name ?? ""))
            {
                error = BotLang.GetLang(BotLangKey.InstanceLoadFail, uuid);
                return true;
            }

            loaded = true;
            EnableInstance(s_instances[uuid]);

            return true;
        }
    }

    /// <summary>
    /// 从磁盘加载一个还没有加载的实例
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    /// <param name="error">失败原因</param>
    internal static bool TryLoad(Guid uuid, out string error)
    {
        error = "";

        if (s_instances.ContainsKey(uuid))
        {
            return true;
        }

        var folder = Path.Combine(BotManager.InstancesPath, uuid.ToString());
        var item = ReadManifest(folder);

        if (item is null)
        {
            error = BotLang.GetLang(BotLangKey.InstanceNotFound, uuid);
            return false;
        }

        lock (s_lock)
        {
            if (s_instances.ContainsKey(uuid))
            {
                return true;
            }

            if (!AddInstance(folder, item.Value.Obj.Proxy, uuid, item.Value.Obj.Name ?? ""))
            {
                error = BotLang.GetLang(BotLangKey.InstanceLoadFail, uuid);
                return false;
            }

            if (StateManager.IsInstanceEnabled(uuid.ToString()))
            {
                EnableInstance(s_instances[uuid]);
            }

            return true;
        }
    }

    /// <summary>
    /// 启用实例
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    /// <param name="error">失败原因</param>
    internal static bool TryEnable(Guid uuid, out string error)
    {
        error = "";

        if (!s_instances.TryGetValue(uuid, out var entry))
        {
            error = BotLang.GetLang(BotLangKey.InstanceNotFound, uuid);
            return false;
        }

        lock (s_lock)
        {
            if (!EnableInstance(entry))
            {
                error = BotLang.GetLang(BotLangKey.InstanceEnableFail, uuid);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 禁用实例
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    /// <param name="error">失败原因</param>
    internal static bool TryDisable(Guid uuid, out string error)
    {
        error = "";

        if (!s_instances.TryGetValue(uuid, out var entry))
        {
            error = BotLang.GetLang(BotLangKey.InstanceNotFound, uuid);
            return false;
        }

        lock (s_lock)
        {
            if (!entry.Enabled)
            {
                StateManager.SetInstanceEnabled(uuid.ToString(), false);
                return true;
            }

            if (!Call(uuid.ToString(), () => entry.Instance.Disable()))
            {
                error = BotLang.GetLang(BotLangKey.InstanceStopFail, uuid);
                return false;
            }

            entry.Enabled = false;
            StateManager.SetInstanceEnabled(uuid.ToString(), false);

            RaiseProxyEvent(entry, ProxyEventType.Stopped);
        }

        return true;
    }

    /// <summary>
    /// 重载实例配置：Disable 实例 → 组件 Reload 实例 → Reload 实例 → Enable 实例
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    /// <param name="error">失败原因</param>
    internal static bool TryReload(Guid uuid, out string error)
    {
        error = "";

        if (!s_instances.TryGetValue(uuid, out var entry))
        {
            error = BotLang.GetLang(BotLangKey.InstanceNotFound, uuid);
            return false;
        }

        lock (s_lock)
        {
            var enabled = entry.Enabled;

            if (enabled)
            {
                if (!Call(uuid.ToString(), () => entry.Instance.Disable()))
                {
                    error = BotLang.GetLang(BotLangKey.InstanceStopFail, uuid);
                    return false;
                }

                entry.Enabled = false;
                RaiseProxyEvent(entry, ProxyEventType.Stopped);
            }

            // 组件把新配置推给实例
            if (ComponentManager.Proxys.TryGetValue(entry.ProxyId, out var proxy))
            {
                try
                {
                    proxy.Reload(entry.Instance);
                }
                catch (Exception e)
                {
                    BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.ComponentActionFail, entry.ProxyId));
                }
            }

            // 实例自己重新读配置
            if (!Call(uuid.ToString(), () =>
            {
                entry.Instance.Reload();
                return true;
            }))
            {
                error = BotLang.GetLang(BotLangKey.InstanceLoadFail, uuid);
                return false;
            }

            if (enabled && !EnableInstance(entry))
            {
                error = BotLang.GetLang(BotLangKey.InstanceEnableFail, uuid);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 移除实例，可选择同时删除实例目录
    /// </summary>
    /// <param name="uuid">实例 uuid</param>
    /// <param name="deleteFolder">是否删除实例目录</param>
    /// <param name="error">失败原因</param>
    internal static bool TryRemove(Guid uuid, bool deleteFolder, out string error)
    {
        error = "";

        var folder = Path.Combine(BotManager.InstancesPath, uuid.ToString());

        if (!s_instances.ContainsKey(uuid) && !Directory.Exists(folder))
        {
            error = BotLang.GetLang(BotLangKey.InstanceNotFound, uuid);
            return false;
        }

        lock (s_lock)
        {
            if (s_instances.TryRemove(uuid, out var entry))
            {
                try
                {
                    if (entry.Enabled)
                    {
                        entry.Instance.Disable();
                    }
                }
                catch (Exception e)
                {
                    BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.InstanceStopFail, uuid));
                }

                if (entry.Enabled)
                {
                    RaiseProxyEvent(entry, ProxyEventType.Stopped);
                }

                ApiRegistry.Unregister(entry.Instance);
                EventManager.Unregister(entry.Instance);
            }

            StateManager.RemoveInstanceState(uuid.ToString());

            if (deleteFolder && !TryDeleteFolder(folder))
            {
                error = BotLang.GetLang(BotLangKey.InstanceRemoveFail, uuid, "目录被占用");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 代理启用后，把它在磁盘上的实例全部加载并启用
    /// </summary>
    /// <param name="proxyId">代理组件 id</param>
    internal static void EnableProxyInstances(string proxyId)
    {
        foreach (var item in EnumerateManifests())
        {
            if (!string.Equals(item.Obj.Proxy, proxyId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            lock (s_lock)
            {
                if (s_instances.ContainsKey(item.Uuid))
                {
                    continue;
                }

                if (!AddInstance(item.Folder, proxyId, item.Uuid, item.Obj.Name ?? ""))
                {
                    continue;
                }

                if (StateManager.IsInstanceEnabled(item.Uuid.ToString()))
                {
                    EnableInstance(s_instances[item.Uuid]);
                }
            }
        }
    }

    /// <summary>
    /// 扫描 Instances 目录创建实例
    /// </summary>
    private static void CreateInstances()
    {
        foreach (var item in EnumerateManifests())
        {
            try
            {
                lock (s_lock)
                {
                    if (!s_instances.ContainsKey(item.Uuid))
                    {
                        AddInstance(item.Folder, item.Obj.Proxy, item.Uuid, item.Obj.Name ?? "");
                    }
                }
            }
            catch (Exception e)
            {
                BotManager.Logger.Error(e, BotLang.GetLang(BotLangKey.InstanceLoadFail, item.Folder));
            }
        }
    }

    /// <summary>
    /// 加载单个实例并放进字典
    /// </summary>
    /// <param name="folder">实例目录</param>
    /// <param name="proxyId">代理组件 id</param>
    /// <param name="uuid">实例 uuid</param>
    /// <param name="name">实例名称</param>
    /// <returns>是否加载成功</returns>
    private static bool AddInstance(string folder, string proxyId, Guid uuid, string name)
    {
        if (!ComponentManager.Proxys.TryGetValue(proxyId, out var proxy))
        {
            BotManager.Logger.Error(BotLang.GetLang(BotLangKey.InstanceProxyMissing, folder, proxyId));
            return false;
        }

        if (proxy.State != ComponentState.Enabled)
        {
            BotManager.Logger.Warn(BotLang.GetLang(BotLangKey.InstanceProxyDisabled, folder, proxyId));
            return false;
        }

        IProxyInstasnce instance;

        try
        {
            instance = proxy.CreateInstance();
            proxy.Reload(instance);

            // 实例配置和 Instance.json 都放在 Instances/{实例uuid}
            instance.Init(folder, uuid);
        }
        catch (Exception e)
        {
            BotManager.Logger.Error(e, BotLang.GetLang(BotLangKey.InstanceLoadFail, folder));
            return false;
        }

        if (!Call(uuid.ToString(), () => instance.Load()))
        {
            BotManager.Logger.Error(BotLang.GetLang(BotLangKey.InstanceLoadFail, folder));
            return false;
        }

        try
        {
            instance.RegisterEventHook();
        }
        catch (Exception e)
        {
            BotManager.Logger.Error(e, BotLang.GetLang(BotLangKey.InstanceActionFail, uuid));
        }

        s_instances[uuid] = new InstanceEntry
        {
            ProxyId = proxyId,
            Name = name,
            Folder = folder,
            Uuid = uuid,
            Instance = instance
        };

        return true;
    }

    /// <summary>
    /// 按启用记录启用实例
    /// </summary>
    private static void EnableInstances()
    {
        foreach (var entry in s_instances.Values)
        {
            if (!StateManager.IsInstanceEnabled(entry.Uuid.ToString()))
            {
                BotManager.Logger.Info(BotLang.GetLang(BotLangKey.InstanceDisabled, entry.Uuid));
                continue;
            }

            EnableInstance(entry);
        }
    }

    /// <summary>
    /// 启用单个实例
    /// </summary>
    /// <param name="entry">实例记录</param>
    /// <returns>是否启用成功</returns>
    private static bool EnableInstance(InstanceEntry entry)
    {
        if (entry.Enabled)
        {
            return true;
        }

        if (!Call(entry.Uuid.ToString(), () => entry.Instance.Enable()))
        {
            BotManager.Logger.Error(BotLang.GetLang(BotLangKey.InstanceEnableFail, entry.Uuid));
            return false;
        }

        entry.Enabled = true;
        StateManager.SetInstanceEnabled(entry.Uuid.ToString(), true);

        RaiseProxyEvent(entry, ProxyEventType.Started);

        return true;
    }

    /// <summary>
    /// 停止并清空所有实例
    /// </summary>
    private static void StopInstances()
    {
        var enabled = s_instances.Values
            .Where(x => x.Enabled)
            .Select(x => x.Uuid.ToString())
            .ToList();

        lock (s_lock)
        {
            foreach (var entry in s_instances.Values)
            {
                try
                {
                    if (entry.Enabled)
                    {
                        entry.Instance.Disable();
                    }
                }
                catch (Exception e)
                {
                    BotManager.Logger.Error(e, BotLang.GetLang(BotLangKey.InstanceStopFail, entry.Uuid));
                }

                if (entry.Enabled)
                {
                    RaiseProxyEvent(entry, ProxyEventType.Stopped);
                }

                ApiRegistry.Unregister(entry.Instance);
                EventManager.Unregister(entry.Instance);
                entry.Enabled = false;
            }

            s_instances.Clear();
        }

        // 关停不算用户主动禁用，恢复启用记录
        foreach (var uuid in enabled)
        {
            StateManager.SetInstanceEnabled(uuid, true);
        }
    }

    /// <summary>
    /// 停止并移除某个代理下的实例
    /// </summary>
    /// <param name="proxyId">代理组件 id</param>
    internal static void StopInstances(string proxyId)
    {
        var entries = s_instances.Values.Where(x => x.ProxyId == proxyId).ToList();

        lock (s_lock)
        {
            foreach (var entry in entries)
            {
                try
                {
                    if (entry.Enabled)
                    {
                        entry.Instance.Disable();
                    }
                }
                catch (Exception e)
                {
                    BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.InstanceStopFail, entry.Uuid));
                }

                if (entry.Enabled)
                {
                    RaiseProxyEvent(entry, ProxyEventType.Stopped);
                }

                ApiRegistry.Unregister(entry.Instance);
                EventManager.Unregister(entry.Instance);
                s_instances.TryRemove(entry.Uuid, out _);
            }
        }
    }

    /// <summary>
    /// 广播实例的 Proxy 事件
    /// </summary>
    /// <param name="entry">实例记录</param>
    /// <param name="type">事件类型</param>
    private static void RaiseProxyEvent(InstanceEntry entry, ProxyEventType type)
    {
        EventManager.HandleProxy(new ProxyEvent
        {
            Id = entry.ProxyId,
            Uuid = entry.Uuid,
            Type = type
        });
    }

    /// <summary>
    /// 读取实例目录下的 Instance.json
    /// </summary>
    /// <param name="folder">实例目录</param>
    /// <returns>实例信息，缺失或无效时返回 null</returns>
    private static (Guid Uuid, string Folder, InstanceObj Obj)? ReadManifest(string folder)
    {
        var manifest = Path.Combine(folder, InstanceManifest);

        if (!File.Exists(manifest))
        {
            return null;
        }

        InstanceObj? obj;

        try
        {
            obj = JsonUtils.ToObj(File.ReadAllText(manifest), SourceGenerationContext.Default.InstanceObj);
        }
        catch (Exception e)
        {
            BotManager.Logger.Error(e, BotLang.GetLang(BotLangKey.InstanceManifestInvalid, folder));
            return null;
        }

        if (obj is null || string.IsNullOrWhiteSpace(obj.Proxy))
        {
            BotManager.Logger.Warn(BotLang.GetLang(BotLangKey.InstanceManifestInvalid, folder));
            return null;
        }

        var uuid = obj.Uuid != Guid.Empty
            ? obj.Uuid
            : Guid.TryParse(Path.GetFileName(folder), out var parsed) ? parsed : Guid.Empty;

        if (uuid == Guid.Empty)
        {
            BotManager.Logger.Warn(BotLang.GetLang(BotLangKey.InstanceManifestInvalid, folder));
            return null;
        }

        return (uuid, folder, obj);
    }

    /// <summary>
    /// 枚举磁盘上的所有实例
    /// </summary>
    private static IEnumerable<(Guid Uuid, string Folder, InstanceObj Obj)> EnumerateManifests()
    {
        var root = BotManager.InstancesPath;

        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var folder in Directory.EnumerateDirectories(root))
        {
            var item = ReadManifest(folder);

            if (item is { } value)
            {
                yield return value;
            }
        }
    }

    /// <summary>
    /// 执行实例代码，异常时记日志并当作失败
    /// </summary>
    /// <param name="id">实例 uuid</param>
    /// <param name="action">实例调用</param>
    private static bool Call(string id, Func<bool> action)
    {
        try
        {
            return action();
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.InstanceActionFail, id));
            return false;
        }
    }

    /// <summary>
    /// 删除目录，失败时返回 false
    /// </summary>
    private static bool TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            return true;
        }
        catch (Exception e)
        {
            BotManager.Logger?.Warn(BotLang.GetLang(BotLangKey.InstanceRemoveFail, folder, e.Message));
            return false;
        }
    }

    /// <summary>
    /// 启动 Tick 定时器
    /// </summary>
    private static void StartTick()
    {
        s_timer = new Timer(_ => Tick(), null, TickInterval, TickInterval);
    }

    /// <summary>
    /// 停止 Tick 定时器
    /// </summary>
    private static void StopTick()
    {
        s_timer?.Dispose();
        s_timer = null;
    }

    /// <summary>
    /// 驱动所有已启用实例的 Tick
    /// </summary>
    private static void Tick()
    {
        foreach (var entry in s_instances.Values)
        {
            if (!entry.Enabled)
            {
                continue;
            }

            try
            {
                entry.Instance.Tick();
            }
            catch (Exception e)
            {
                BotManager.Logger.Error(e, BotLang.GetLang(BotLangKey.InstanceTickError, entry.Uuid));
            }
        }
    }
}