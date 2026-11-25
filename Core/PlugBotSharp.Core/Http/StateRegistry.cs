using System.Collections.Concurrent;

namespace PlugBotSharp.Core.Http;

/// <summary>
/// 组件上报的自定义状态，面板上展示。
/// 两种形态：单值状态（value）一个名字一个值，键值状态（kv）一个名字一组成对的键和值
/// </summary>
internal static class StateRegistry
{
    /// <summary>
    /// 单值状态的类型名
    /// </summary>
    public const string ValueKind = "value";

    /// <summary>
    /// 键值状态的类型名
    /// </summary>
    public const string KvKind = "kv";

    private static readonly ConcurrentDictionary<object, ConcurrentDictionary<string, StateItem>> s_states = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// 声明一个状态，注册阶段调用：先把名字、显示名和描述记下来，面板上就能看到，
    /// 值还没上报时显示成空
    /// </summary>
    /// <param name="owner">组件对象</param>
    /// <param name="name">状态名，面板接口里当键用</param>
    /// <param name="kind">类型，value 单值 / kv 键值</param>
    /// <param name="label">面板上显示的名字</param>
    /// <param name="description">面板上的解释</param>
    public static void Declare(object owner, string name, string kind, string label, string description)
    {
        var map = s_states.GetOrAdd(owner, _ => new ConcurrentDictionary<string, StateItem>());
        var item = map.GetOrAdd(name, _ => new StateItem { Kind = kind });

        item.Label = label;
        item.Description = description;
    }

    /// <summary>
    /// 设置单值状态
    /// </summary>
    /// <param name="owner">组件对象</param>
    /// <param name="name">状态名</param>
    /// <param name="value">状态值，基础类型或 null</param>
    public static void SetValue(object owner, string name, object? value)
    {
        var map = s_states.GetOrAdd(owner, _ => new ConcurrentDictionary<string, StateItem>());
        var item = map.GetOrAdd(name, _ => new StateItem { Kind = ValueKind });

        // 同一个名字先当键值用、后来当单值用，把键值丢掉
        item.Kind = ValueKind;
        item.Kv.Clear();
        item.Value = value;
    }

    /// <summary>
    /// 设置键值状态的一个键，状态不存在时按键值状态新建
    /// </summary>
    /// <param name="owner">组件对象</param>
    /// <param name="name">状态名</param>
    /// <param name="key">键</param>
    /// <param name="value">值，基础类型或 null</param>
    public static void SetKey(object owner, string name, string key, object? value)
    {
        var map = s_states.GetOrAdd(owner, _ => new ConcurrentDictionary<string, StateItem>());
        var item = GetKv(map, name);

        item.Kv[key] = value;
    }

    /// <summary>
    /// 删掉键值状态的一个键
    /// </summary>
    /// <param name="owner">组件对象</param>
    /// <param name="name">状态名</param>
    /// <param name="key">键</param>
    public static void RemoveKey(object owner, string name, string key)
    {
        if (s_states.TryGetValue(owner, out var map) && map.TryGetValue(name, out var item))
        {
            item.Kv.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// 清空键值状态的键，状态本身留着
    /// </summary>
    /// <param name="owner">组件对象</param>
    /// <param name="name">状态名</param>
    public static void ClearKeys(object owner, string name)
    {
        if (s_states.TryGetValue(owner, out var map) && map.TryGetValue(name, out var item))
        {
            item.Kv.Clear();
        }
    }

    /// <summary>
    /// 取某个组件的全部状态，返回的是快照，之后改状态不影响已经拿到的结果
    /// </summary>
    /// <param name="owner">组件对象</param>
    public static IReadOnlyDictionary<string, StateView> Get(object? owner)
    {
        var result = new Dictionary<string, StateView>();

        if (owner is null || !s_states.TryGetValue(owner, out var map))
        {
            return result;
        }

        foreach (var (name, item) in map)
        {
            result[name] = item.Kind == KvKind
                ? new StateView(item.Label, item.Description, KvKind, new Dictionary<string, object?>(item.Kv))
                : new StateView(item.Label, item.Description, ValueKind, item.Value);
        }

        return result;
    }

    /// <summary>
    /// 移除某个组件上报的状态，组件卸载时调用
    /// </summary>
    /// <param name="owner">组件对象</param>
    public static void Remove(object owner)
    {
        s_states.TryRemove(owner, out _);
    }

    /// <summary>
    /// 移除单个状态
    /// </summary>
    /// <param name="owner">组件对象</param>
    /// <param name="name">状态名</param>
    public static void Remove(object owner, string name)
    {
        if (s_states.TryGetValue(owner, out var map))
        {
            map.TryRemove(name, out _);
        }
    }

    /// <summary>
    /// 清空全部状态，只给测试用
    /// </summary>
    internal static void Clear()
    {
        s_states.Clear();
    }

    /// <summary>
    /// 取键值状态，没有就按键值状态新建
    /// </summary>
    /// <param name="map">组件的状态表</param>
    /// <param name="name">状态名</param>
    private static StateItem GetKv(ConcurrentDictionary<string, StateItem> map, string name)
    {
        var created = new StateItem { Kind = KvKind };
        var item = map.GetOrAdd(name, _ => created);

        // 同一个名字先当单值用、后来当键值用，把单值丢掉
        item.Kind = KvKind;
        item.Value = null;

        return item;
    }

    /// <summary>
    /// 一个状态：单值存 Value，键值存 Kv，另外记下面板上显示的名字和描述
    /// </summary>
    private sealed class StateItem
    {
        /// <summary>
        /// 类型，value 或 kv
        /// </summary>
        public string Kind { get; set; } = ValueKind;

        /// <summary>
        /// 面板上显示的名字
        /// </summary>
        public string Label { get; set; } = "";

        /// <summary>
        /// 面板上的解释
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// 单值状态的值
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// 键值状态的键和值
        /// </summary>
        public ConcurrentDictionary<string, object?> Kv { get; } = new();
    }
}

/// <summary>
/// 面板上的一个状态，名字是它在字典里的键
/// </summary>
/// <param name="Label">面板上显示的名字</param>
/// <param name="Description">面板上的解释</param>
/// <param name="Kind">类型，value 单值 / kv 键值</param>
/// <param name="Value">单值是基础类型，键值是 键 -&gt; 值</param>
internal sealed record StateView(string Label, string Description, string Kind, object? Value);
