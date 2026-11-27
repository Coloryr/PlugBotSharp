using PlugBotSharp.Api.Configs;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core.Components;

/// <summary>
/// 组件/实例启用状态记录，只由宿主内部维护，组件不能直接调用
/// </summary>
internal static class StateManager
{
    private static readonly Lock s_lock = new();

    private static string s_componentFile = "";
    private static string s_instanceFile = "";

    /// <summary>
    /// 组件启用状态
    /// </summary>
    internal static ComponentStateObj ComponentState { get; } = new();

    /// <summary>
    /// 实例启用状态
    /// </summary>
    internal static InstanceStateObj InstanceState { get; } = new();

    /// <summary>
    /// 初始化状态文件
    /// </summary>
    public static void Init()
    {
        s_componentFile = Path.Combine(BotManager.ConfigsPath, "ComponentState.json");
        s_instanceFile = Path.Combine(BotManager.ConfigsPath, "InstanceState.json");
    }

    /// <summary>
    /// 读取状态文件
    /// </summary>
    public static void Load()
    {
        LoadComponentState();
        LoadInstanceState();
    }

    /// <summary>
    /// 保存全部状态
    /// </summary>
    public static void Save()
    {
        lock (s_lock)
        {
            SaveComponentStateLocked();
            SaveInstanceStateLocked();
        }
    }

    /// <summary>
    /// 组件是否启用，没有记录时默认启用
    /// </summary>
    public static bool IsComponentEnabled(string id)
    {
        lock (s_lock)
        {
            return !ComponentState.Components.TryGetValue(id, out var enabled) || enabled;
        }
    }

    /// <summary>
    /// 记录组件启用状态
    /// </summary>
    public static void SetComponentEnabled(string id, bool enabled)
    {
        bool changed;
        lock (s_lock)
        {
            changed = !ComponentState.Components.TryGetValue(id, out var current) || current != enabled;

            if (changed)
            {
                ComponentState.Components[id] = enabled;
                SaveComponentStateLocked();
            }
        }

        if (changed)
        {
            BotManager.Logger?.Info(BotLang.GetLang(BotLangKey.ComponentStateChanged, id, enabled));
        }
    }

    /// <summary>
    /// 组件第一次加载时补一条默认启用记录
    /// </summary>
    internal static void EnsureComponentState(string id)
    {
        lock (s_lock)
        {
            if (ComponentState.Components.TryAdd(id, true))
            {
                SaveComponentStateLocked();
            }
        }
    }

    /// <summary>
    /// 实例是否启用，没有记录时默认启用
    /// </summary>
    public static bool IsInstanceEnabled(string id)
    {
        lock (s_lock)
        {
            return !InstanceState.Instances.TryGetValue(id, out var enabled) || enabled;
        }
    }

    /// <summary>
    /// 记录实例启用状态
    /// </summary>
    public static void SetInstanceEnabled(string id, bool enabled)
    {
        bool changed;
        lock (s_lock)
        {
            changed = !InstanceState.Instances.TryGetValue(id, out var current) || current != enabled;

            if (changed)
            {
                InstanceState.Instances[id] = enabled;
                SaveInstanceStateLocked();
            }
        }

        if (changed)
        {
            BotManager.Logger?.Info(BotLang.GetLang(BotLangKey.InstanceStateChanged, id, enabled));
        }
    }

    /// <summary>
    /// 删除实例的启用记录，实例被移除时调用
    /// </summary>
    /// <param name="id">实例 uuid</param>
    internal static void RemoveInstanceState(string id)
    {
        lock (s_lock)
        {
            if (InstanceState.Instances.Remove(id))
            {
                SaveInstanceStateLocked();
            }
        }
    }

    /// <summary>
    /// 删除组件的启用记录，组件被删除时调用
    /// </summary>
    /// <param name="id">组件 id</param>
    internal static void RemoveComponentState(string id)
    {
        lock (s_lock)
        {
            if (ComponentState.Components.Remove(id))
            {
                SaveComponentStateLocked();
            }
        }
    }

    /// <summary>
    /// 读取组件启用状态
    /// </summary>
    private static void LoadComponentState()
    {
        try
        {
            var state = File.Exists(s_componentFile)
                ? JsonUtils.ToObj(File.ReadAllText(s_componentFile), SourceGenerationContext.Default.ComponentStateObj)
                : null;

            lock (s_lock)
            {
                ComponentState.Components = state?.Components ?? [];
            }
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.ErrorConfigRead));
        }
    }

    /// <summary>
    /// 保存组件启用状态，调用方需要持有 s_lock
    /// </summary>
    private static void SaveComponentStateLocked()
    {
        try
        {
            WriteFile(s_componentFile, JsonUtils.ToString(ComponentState, SourceGenerationContext.Default.ComponentStateObj));
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.ErrorConfigSave));
        }
    }

    /// <summary>
    /// 读取实例启用状态
    /// </summary>
    private static void LoadInstanceState()
    {
        try
        {
            var state = File.Exists(s_instanceFile)
                ? JsonUtils.ToObj(File.ReadAllText(s_instanceFile), SourceGenerationContext.Default.InstanceStateObj)
                : null;

            lock (s_lock)
            {
                InstanceState.Instances = state?.Instances ?? [];
            }
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.ErrorConfigRead));
        }
    }

    /// <summary>
    /// 保存实例启用状态，调用方需要持有 s_lock
    /// </summary>
    private static void SaveInstanceStateLocked()
    {
        try
        {
            WriteFile(s_instanceFile, JsonUtils.ToString(InstanceState, SourceGenerationContext.Default.InstanceStateObj));
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.ErrorConfigSave));
        }
    }

    /// <summary>
    /// 先写临时文件再替换，避免进程中途退出留下半个文件
    /// </summary>
    /// <param name="file">目标文件</param>
    /// <param name="text">内容</param>
    private static void WriteFile(string file, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        var temp = file + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, file, true);
    }
}
