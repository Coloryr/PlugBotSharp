using System.Collections.Concurrent;

namespace PlugBotSharp.Core.Http;

/// <summary>
/// 组件上报的自定义状态，面板上展示
/// </summary>
internal static class StateRegistry
{
    private static readonly ConcurrentDictionary<object, ConcurrentDictionary<string, string>> s_states = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// 设置一个状态
    /// </summary>
    /// <param name="owner">组件对象</param>
    /// <param name="name">状态名</param>
    /// <param name="value">状态值</param>
    public static void Set(object owner, string name, string value)
    {
        s_states.GetOrAdd(owner, _ => new ConcurrentDictionary<string, string>()).AddOrUpdate(name, value, (_, _) => value);
    }

    /// <summary>
    /// 取某个组件的全部状态
    /// </summary>
    /// <param name="owner">组件对象</param>
    public static IReadOnlyDictionary<string, string> Get(object? owner)
    {
        if (owner is not null && s_states.TryGetValue(owner, out var map))
        {
            return new Dictionary<string, string>(map);
        }

        return new Dictionary<string, string>();
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
}
