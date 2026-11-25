using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlugBotSharp.Core.Http;

/// <summary>
/// 组件上报的自定义状态，面板上展示，同时由宿主自动落盘，重启后接着用。
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

    private static readonly ConcurrentDictionary<object, OwnerStates> s_states = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// 每隔两秒把改过的状态写一次盘
    /// </summary>
    private static readonly Timer s_timer = new(_ => Flush(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

    /// <summary>
    /// 声明一个状态，注册阶段调用：把名字、显示名和描述记下来，同时把上次落盘的值读回来
    /// </summary>
    /// <param name="owner">组件对象</param>
    /// <param name="name">状态名，面板接口里当键用</param>
    /// <param name="kind">类型，value 单值 / kv 键值</param>
    /// <param name="label">面板上显示的名字</param>
    /// <param name="description">面板上的解释</param>
    /// <param name="storePath">落盘路径，为空就不存</param>
    public static void Declare(object owner, string name, string kind, string label, string description, string storePath = "")
    {
        var states = s_states.GetOrAdd(owner, _ => new OwnerStates(storePath));

        states.Load();

        var item = states.Items.GetOrAdd(name, _ => new StateItem { Kind = kind });

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
        var states = s_states.GetOrAdd(owner, _ => new OwnerStates(""));
        var item = states.Items.GetOrAdd(name, _ => new StateItem { Kind = ValueKind });

        // 同一个名字先当键值用、后来当单值用，把键值丢掉
        item.Kind = ValueKind;
        item.Kv.Clear();
        item.Value = value;

        states.Dirty = true;
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
        var states = s_states.GetOrAdd(owner, _ => new OwnerStates(""));
        var item = GetKv(states, name);

        item.Kv[key] = value;

        states.Dirty = true;
    }

    /// <summary>
    /// 删掉键值状态的一个键
    /// </summary>
    /// <param name="owner">组件对象</param>
    /// <param name="name">状态名</param>
    /// <param name="key">键</param>
    public static void RemoveKey(object owner, string name, string key)
    {
        if (s_states.TryGetValue(owner, out var states) && states.Items.TryGetValue(name, out var item))
        {
            item.Kv.TryRemove(key, out _);

            states.Dirty = true;
        }
    }

    /// <summary>
    /// 清空键值状态的键，状态本身留着
    /// </summary>
    /// <param name="owner">组件对象</param>
    /// <param name="name">状态名</param>
    public static void ClearKeys(object owner, string name)
    {
        if (s_states.TryGetValue(owner, out var states) && states.Items.TryGetValue(name, out var item))
        {
            item.Kv.Clear();

            states.Dirty = true;
        }
    }

    /// <summary>
    /// 取某个组件的全部状态，返回的是快照，之后改状态不影响已经拿到的结果
    /// </summary>
    /// <param name="owner">组件对象</param>
    public static IReadOnlyDictionary<string, StateView> Get(object? owner)
    {
        var result = new Dictionary<string, StateView>();

        if (owner is null || !s_states.TryGetValue(owner, out var states))
        {
            return result;
        }

        foreach (var (name, item) in states.Items)
        {
            result[name] = item.Kind == KvKind
                ? new StateView(item.Label, item.Description, KvKind, new Dictionary<string, object?>(item.Kv))
                : new StateView(item.Label, item.Description, ValueKind, item.Value);
        }

        return result;
    }

    /// <summary>
    /// 取一个状态当前的值，注册时用来接着上次的累计值
    /// </summary>
    /// <param name="owner">组件对象</param>
    /// <param name="name">状态名</param>
    public static object? GetValue(object owner, string name)
    {
        if (!s_states.TryGetValue(owner, out var states) || !states.Items.TryGetValue(name, out var item))
        {
            return null;
        }

        return item.Kind == KvKind ? new Dictionary<string, object?>(item.Kv) : item.Value;
    }

    /// <summary>
    /// 移除某个组件上报的状态，组件卸载时调用；移之前先把没写的状态落盘
    /// </summary>
    /// <param name="owner">组件对象</param>
    public static void Remove(object owner)
    {
        if (s_states.TryRemove(owner, out var states) && states.Dirty)
        {
            states.Save();
        }
    }

    /// <summary>
    /// 移除单个状态
    /// </summary>
    /// <param name="owner">组件对象</param>
    /// <param name="name">状态名</param>
    public static void Remove(object owner, string name)
    {
        if (s_states.TryGetValue(owner, out var states))
        {
            states.Items.TryRemove(name, out _);
            states.Dirty = true;
        }
    }

    /// <summary>
    /// 把所有改过的状态写盘，宿主退出前也会调一次
    /// </summary>
    public static void Flush()
    {
        foreach (var (_, states) in s_states)
        {
            if (states.Dirty)
            {
                states.Save();
            }
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
    /// <param name="states">组件的状态表</param>
    /// <param name="name">状态名</param>
    private static StateItem GetKv(OwnerStates states, string name)
    {
        var item = states.Items.GetOrAdd(name, _ => new StateItem { Kind = KvKind });

        // 同一个名字先当单值用、后来当键值用，把单值丢掉
        item.Kind = KvKind;
        item.Value = null;

        return item;
    }

    /// <summary>
    /// 值转成 json 节点
    /// </summary>
    /// <param name="value">基础类型的值</param>
    private static JsonNode? ToNode(object? value)
    {
        return value switch
        {
            null => null,
            string text => JsonValue.Create(text),
            bool flag => JsonValue.Create(flag),
            char ch => JsonValue.Create(ch.ToString()),
            byte or sbyte or short or ushort or int or uint or long => JsonValue.Create(Convert.ToInt64(value)),
            ulong big => JsonValue.Create(big),
            float single => JsonValue.Create(single),
            double number => JsonValue.Create(number),
            decimal money => JsonValue.Create(money),
            _ => JsonValue.Create(value.ToString())
        };
    }

    /// <summary>
    /// json 节点转回基础类型的值
    /// </summary>
    /// <param name="node">json 节点</param>
    private static object? ToValue(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<bool>(out var flag))
        {
            return flag;
        }

        if (value.TryGetValue<long>(out var number))
        {
            return number;
        }

        if (value.TryGetValue<double>(out var real))
        {
            return real;
        }

        if (value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return value.ToJsonString();
    }

    /// <summary>
    /// 一个组件的全部状态，以及它在磁盘上的位置
    /// </summary>
    private sealed class OwnerStates(string storePath)
    {
        /// <summary>
        /// 落盘路径，为空就只在内存里
        /// </summary>
        public string StorePath { get; } = storePath;

        /// <summary>
        /// 状态表
        /// </summary>
        public ConcurrentDictionary<string, StateItem> Items { get; } = new();

        /// <summary>
        /// 有没有改过还没写盘
        /// </summary>
        public bool Dirty { get; set; }

        private readonly Lock _lock = new();
        private bool _loaded;

        /// <summary>
        /// 把上次落盘的值读回来，一个组件只读一次
        /// </summary>
        public void Load()
        {
            lock (_lock)
            {
                if (_loaded)
                {
                    return;
                }

                _loaded = true;

                if (string.IsNullOrEmpty(StorePath) || !File.Exists(StorePath))
                {
                    return;
                }

                try
                {
                    if (JsonNode.Parse(File.ReadAllText(StorePath)) is not JsonObject root)
                    {
                        return;
                    }

                    foreach (var (name, node) in root)
                    {
                        if (node is not JsonObject obj)
                        {
                            continue;
                        }

                        var kind = obj["kind"]?.GetValue<string>() ?? ValueKind;
                        var item = Items.GetOrAdd(name, _ => new StateItem { Kind = kind });

                        item.Kind = kind;

                        if (kind == KvKind)
                        {
                            if (obj["value"] is JsonObject kv)
                            {
                                foreach (var (key, value) in kv)
                                {
                                    item.Kv[key] = ToValue(value);
                                }
                            }
                        }
                        else
                        {
                            item.Value = ToValue(obj["value"]);
                        }
                    }
                }
                catch (Exception e)
                {
                    BotManager.Logger?.Warn(e, "状态文件读取失败: " + StorePath);
                }
            }
        }

        /// <summary>
        /// 把状态写回磁盘
        /// </summary>
        public void Save()
        {
            lock (_lock)
            {
                Dirty = false;

                if (string.IsNullOrEmpty(StorePath))
                {
                    return;
                }

                try
                {
                    var root = new JsonObject();

                    foreach (var (name, item) in Items)
                    {
                        var obj = new JsonObject { ["kind"] = item.Kind };

                        if (item.Kind == KvKind)
                        {
                            var kv = new JsonObject();

                            foreach (var (key, value) in item.Kv)
                            {
                                kv[key] = ToNode(value);
                            }

                            obj["value"] = kv;
                        }
                        else
                        {
                            obj["value"] = ToNode(item.Value);
                        }

                        root[name] = obj;
                    }

                    var dir = Path.GetDirectoryName(StorePath);

                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.WriteAllText(StorePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                }
                catch (Exception e)
                {
                    BotManager.Logger?.Warn(e, "状态文件写入失败: " + StorePath);
                }
            }
        }
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
