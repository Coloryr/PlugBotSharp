using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.Core;

/// <summary>
/// Proxy 事件类型
/// </summary>
public enum ProxyEventType
{
    /// <summary>
    /// 实例已启用，可以收发消息
    /// </summary>
    Started,
    /// <summary>
    /// 实例已停用
    /// </summary>
    Stopped
}

/// <summary>
/// Proxy 事件：代理组件或实例的状态变化
/// </summary>
public record ProxyEvent
{
    /// <summary>
    /// 代理组件 id
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// 实例 uuid，组件级事件为 Guid.Empty
    /// </summary>
    public required Guid Uuid { get; init; }
    /// <summary>
    /// 事件类型
    /// </summary>
    public required ProxyEventType Type { get; init; }
    /// <summary>
    /// 附加说明，一般是失败原因
    /// </summary>
    public string Detail { get; init; } = "";
}

/// <summary>
/// 插件事件类型
/// </summary>
public enum PluginEventType
{
    /// <summary>
    /// 已加载
    /// </summary>
    Loaded,
    /// <summary>
    /// 已启用
    /// </summary>
    Enabled,
    /// <summary>
    /// 已禁用
    /// </summary>
    Disabled,
    /// <summary>
    /// 已卸载
    /// </summary>
    Unloaded,
    /// <summary>
    /// 自定义事件，名字在 Name 里
    /// </summary>
    Custom
}

/// <summary>
/// 插件事件：生命周期变化或者插件之间自己发的事件
/// </summary>
public record PluginEvent
{
    /// <summary>
    /// 事件来源的插件 id
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// 事件类型
    /// </summary>
    public required PluginEventType Type { get; init; }
    /// <summary>
    /// 自定义事件名
    /// </summary>
    public string Name { get; init; } = "";
    /// <summary>
    /// 事件数据
    /// </summary>
    public object? Data { get; init; }
}

/// <summary>
/// 事件中心：消息事件、Proxy 事件、插件事件
/// </summary>
internal static class EventManager
{
    private sealed record Hook(object Owner, Delegate Handler);

    /// <summary>
    /// 保护三个注册表，注册和卸载都在组件的加载线程里
    /// </summary>
    private static readonly Lock s_lock = new();

    private static readonly List<Hook> s_messages = [];
    private static readonly List<Hook> s_proxys = [];
    private static readonly List<Hook> s_plugins = [];
    private static readonly List<Hook> s_sends = [];

    /// <summary>
    /// 注册消息事件，代理收到消息后由它调用，返回值是要回复的消息
    /// </summary>
    /// <param name="owner">注册者，卸载时用来清理</param>
    /// <param name="handler">回调</param>
    public static void RegisterMessageHook(object owner, Func<ProxyMessage, List<BaseMessage>> handler)
    {
        Add(s_messages, owner, handler);
    }

    /// <summary>
    /// 注册 Proxy 事件
    /// </summary>
    /// <param name="owner">注册者，卸载时用来清理</param>
    /// <param name="handler">回调</param>
    public static void RegisterProxyHook(object owner, Action<ProxyEvent> handler)
    {
        Add(s_proxys, owner, handler);
    }

    /// <summary>
    /// 注册插件事件
    /// </summary>
    /// <param name="owner">注册者，卸载时用来清理</param>
    /// <param name="handler">回调</param>
    public static void RegisterPluginHook(object owner, Action<PluginEvent> handler)
    {
        Add(s_plugins, owner, handler);
    }

    /// <summary>
    /// 注册发送钩子：别的组件要把消息发到某个实例时，通过这里转交
    /// </summary>
    /// <param name="owner">注册者，卸载时用来清理</param>
    /// <param name="handler">回调，收到的是带目标实例的上下文和要发的消息</param>
    public static void RegisterSendHook(object owner, Func<ProxyMessage, BaseMessage, Task> handler)
    {
        Add(s_sends, owner, handler);
    }

    /// <summary>
    /// 移除某个组件的全部注册，组件卸载时调用
    /// </summary>
    /// <param name="owner">注册者</param>
    /// <returns>是否移除了注册</returns>
    public static bool Unregister(object owner)
    {
        lock (s_lock)
        {
            var removed = Remove(s_messages, owner);
            removed |= Remove(s_proxys, owner);
            removed |= Remove(s_plugins, owner);
            removed |= Remove(s_sends, owner);

            return removed;
        }
    }

    /// <summary>
    /// 处理消息事件，把所有插件的回复拼起来
    /// </summary>
    /// <param name="id">代理组件 id</param>
    /// <param name="instance">实例 uuid</param>
    /// <param name="message">收到的消息</param>
    /// <returns>要回复的消息</returns>
    public static List<BaseMessage> HandleMessage(string id, Guid instance, BaseMessage message)
    {
        return HandleMessage(new ProxyMessage
        {
            Id = id,
            Uuid = instance,
            SenderType = MessageSenderType.Any,
            Sender = "",
            Message = message
        });
    }

    /// <summary>
    /// 处理消息事件，把所有插件的回复拼起来
    /// </summary>
    /// <param name="message">收到的消息，带发送者信息</param>
    /// <returns>要回复的消息</returns>
    public static List<BaseMessage> HandleMessage(ProxyMessage message)
    {
        var result = new List<BaseMessage>();

        foreach (var hook in Snapshot(s_messages))
        {
            try
            {
                if (((Func<ProxyMessage, List<BaseMessage>>)hook.Handler)(message) is { Count: > 0 } replies)
                {
                    result.AddRange(replies);
                }
            }
            catch (Exception e)
            {
                BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.EventMessageFail, message.Id));
            }
        }

        return result;
    }

    /// <summary>
    /// 把一条消息交给目标实例发出去，由实例自己在发送钩子里过滤
    /// </summary>
    /// <param name="message">消息上下文，Uuid 是目标实例</param>
    /// <param name="reply">要发的消息</param>
    public static async Task HandleSendAsync(ProxyMessage message, BaseMessage reply)
    {
        foreach (var hook in Snapshot(s_sends))
        {
            try
            {
                await ((Func<ProxyMessage, BaseMessage, Task>)hook.Handler)(message, reply);
            }
            catch (Exception e)
            {
                BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.EventProxyFail, message.Id));
            }
        }
    }

    /// <summary>
    /// 广播 Proxy 事件
    /// </summary>
    /// <param name="evt">事件</param>
    public static void HandleProxy(ProxyEvent evt)
    {
        foreach (var hook in Snapshot(s_proxys))
        {
            try
            {
                ((Action<ProxyEvent>)hook.Handler)(evt);
            }
            catch (Exception e)
            {
                BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.EventProxyFail, evt.Id));
            }
        }
    }

    /// <summary>
    /// 广播插件事件
    /// </summary>
    /// <param name="evt">事件</param>
    public static void HandlePlugin(PluginEvent evt)
    {
        foreach (var hook in Snapshot(s_plugins))
        {
            try
            {
                ((Action<PluginEvent>)hook.Handler)(evt);
            }
            catch (Exception e)
            {
                BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.EventPluginFail, evt.Id));
            }
        }
    }

    /// <summary>
    /// 触发自定义插件事件，插件之间通信走这里
    /// </summary>
    /// <param name="id">来源插件 id</param>
    /// <param name="name">事件名</param>
    /// <param name="data">事件数据</param>
    public static void RaisePlugin(string id, string name, object? data = null)
    {
        HandlePlugin(new PluginEvent
        {
            Id = id,
            Type = PluginEventType.Custom,
            Name = name,
            Data = data
        });
    }

    /// <summary>
    /// 清空全部注册，只给测试用
    /// </summary>
    internal static void Clear()
    {
        lock (s_lock)
        {
            s_messages.Clear();
            s_proxys.Clear();
            s_plugins.Clear();
            s_sends.Clear();
        }
    }

    /// <summary>
    /// 注册，同一个 owner 同一个回调重复注册只算一次
    /// </summary>
    private static void Add(List<Hook> hooks, object owner, Delegate handler)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);

        lock (s_lock)
        {
            if (hooks.Contains(new Hook(owner, handler)))
            {
                return;
            }

            hooks.Add(new Hook(owner, handler));
        }
    }

    /// <summary>
    /// 移除某个 owner 的注册
    /// </summary>
    private static bool Remove(List<Hook> hooks, object owner)
    {
        return hooks.RemoveAll(x => ReferenceEquals(x.Owner, owner)) > 0;
    }

    /// <summary>
    /// 取快照再回调，避免回调里注册 / 卸载时死锁
    /// </summary>
    private static Hook[] Snapshot(List<Hook> hooks)
    {
        lock (s_lock)
        {
            return [.. hooks];
        }
    }
}