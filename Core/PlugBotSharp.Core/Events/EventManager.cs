using System.Text.Json.Nodes;
using PlugBotSharp.Core.Http;
using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.Core;

/// <summary>
/// 事件中心：消息事件、Proxy 事件、插件事件
/// </summary>
internal static class EventManager
{
    /// <summary>
    /// 一条订阅，SourceId / EventId 为空表示广播订阅，填了就是定向订阅，值可以是 *
    /// </summary>
    private sealed record Hook(object Owner, Delegate Handler, string? SourceId = null, string? EventId = null);

    /// <summary>
    /// 保护三个注册表，注册和卸载都在组件的加载线程里
    /// </summary>
    private static readonly Lock s_lock = new();

    private static readonly List<Hook> s_messages = [];
    private static readonly List<Hook> s_messageTasks = [];
    private static readonly List<Hook> s_proxys = [];
    private static readonly List<Hook> s_plugins = [];
    private static readonly List<Hook> s_sends = [];
    private static readonly List<Hook> s_sendFilters = [];

    /// <summary>
    /// 订阅消息事件，代理收到消息后由它调用，返回值是要回复的消息
    /// </summary>
    /// <param name="owner">订阅者，卸载时用来清理</param>
    /// <param name="handler">回调</param>
    public static void SubscribeMessage(object owner, Func<ProxyMessage, List<BaseMessage>> handler)
    {
        Add(s_messages, owner, handler);
    }

    /// <summary>
    /// 订阅消息事件（长任务版），回调在后台跑，处理完自己调 Reply
    /// </summary>
    /// <param name="owner">订阅者，卸载时用来清理</param>
    /// <param name="handler">回调</param>
    public static void SubscribeMessage(object owner, Func<MessageEvent, Task> handler)
    {
        Add(s_messageTasks, owner, handler);
    }

    /// <summary>
    /// 订阅 Proxy 事件
    /// </summary>
    /// <param name="owner">订阅者，卸载时用来清理</param>
    /// <param name="handler">回调</param>
    public static void SubscribeProxy(object owner, Action<ProxyEvent> handler)
    {
        Add(s_proxys, owner, handler);
    }

    /// <summary>
    /// 订阅插件事件，收全部：生命周期事件和所有自定义事件
    /// </summary>
    /// <param name="owner">订阅者，卸载时用来清理</param>
    /// <param name="handler">回调</param>
    public static void SubscribePlugin(object owner, Action<PluginEvent> handler)
    {
        Add(s_plugins, owner, handler);
    }

    /// <summary>
    /// 订阅自定义事件，组件 id 和事件 id 都可以用 * 通配
    /// </summary>
    /// <param name="owner">订阅者，卸载时用来清理</param>
    /// <param name="componentId">发出事件的组件 id，* 表示全部</param>
    /// <param name="eventId">事件 id，* 表示全部</param>
    /// <param name="handler">回调</param>
    public static void SubscribePluginEvent(object owner, string componentId, string eventId, Action<PluginEvent> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        Add(s_plugins, owner, handler, componentId, eventId);
    }

    /// <summary>
    /// 订阅发送前的拦截器，返回 true 取消这次发送
    /// </summary>
    /// <param name="owner">订阅者，卸载时用来清理</param>
    /// <param name="handler">回调，返回 true 就是拦截</param>
    public static void SubscribeSendFilter(object owner, Func<ProxyMessage, BaseMessage, Task<bool>> handler)
    {
        Add(s_sendFilters, owner, handler);
    }

    /// <summary>
    /// 注册实例自己的发送实现，拦截器都放行之后才会调到
    /// </summary>
    /// <param name="owner">实例，卸载时用来清理</param>
    /// <param name="handler">回调，真正把消息发到平台</param>
    public static void RegisterSendTarget(object owner, Func<ProxyMessage, BaseMessage, Task> handler)
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
            removed |= Remove(s_messageTasks, owner);
            removed |= Remove(s_proxys, owner);
            removed |= Remove(s_plugins, owner);
            removed |= Remove(s_sends, owner);
            removed |= Remove(s_sendFilters, owner);

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
    /// <param name="target">只发给这个所有者（插件 / 代理 / 实例），为空时发给所有人</param>
    /// <returns>要回复的消息</returns>
    public static List<BaseMessage> HandleMessage(ProxyMessage message, object? target = null)
    {
        var result = new List<BaseMessage>();

        foreach (var hook in Snapshot(s_messages))
        {
            if (target is not null && !ReferenceEquals(hook.Owner, target))
            {
                continue;
            }

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

        // 长任务版：不阻塞，回调在后台跑，结束后自己把消息发回发起方
        if (s_messageTasks.Count > 0)
        {
            var evt = new MessageEvent(message, reply => HandleSendAsync(message, reply));

            foreach (var hook in Snapshot(s_messageTasks))
            {
                if (target is not null && !ReferenceEquals(hook.Owner, target))
                {
                    continue;
                }

                _ = RunMessageHookAsync(hook, evt, message.Id);
            }
        }

        return result;
    }

    /// <summary>
    /// 跑一个长任务版的消息回调，异常只记日志
    /// </summary>
    /// <param name="hook">订阅</param>
    /// <param name="evt">消息事件</param>
    /// <param name="id">来源代理 id</param>
    private static async Task RunMessageHookAsync(Hook hook, MessageEvent evt, string id)
    {
        try
        {
            await ((Func<MessageEvent, Task>)hook.Handler)(evt);
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.EventMessageFail, id));
        }
    }

    /// <summary>
    /// 把一条消息交给目标实例发出去：先跑拦截器，全部放行才真正发
    /// </summary>
    /// <param name="message">消息上下文，Uuid 是目标实例</param>
    /// <param name="reply">要发的消息</param>
    public static async Task HandleSendAsync(ProxyMessage message, BaseMessage reply)
    {
        foreach (var hook in Snapshot(s_sendFilters))
        {
            try
            {
                if (await ((Func<ProxyMessage, BaseMessage, Task<bool>>)hook.Handler)(message, reply))
                {
                    BotManager.Logger?.Debug(BotLang.GetLang(BotLangKey.SendCanceled, message.Id));

                    return;
                }
            }
            catch (Exception e)
            {
                BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.EventProxyFail, message.Id));
            }
        }

        // 面板调试：发往调试终端的回复拦下来给面板，不落到真实平台
        if (message.Uuid == DebugSink.Uuid)
        {
            DebugSink.Capture(reply);

            return;
        }

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
            // 定向订阅只认自定义事件，组件 id / 事件 id 按 * 通配
            if (!Matches(hook, evt))
            {
                continue;
            }

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
    /// <param name="data">事件数据，基础类型或 JSON 节点</param>
    /// <param name="message">事件带的消息，和 data 二选一</param>
    public static void RaisePlugin(string id, string name, object? data = null, BaseMessage? message = null)
    {
        HandlePlugin(new PluginEvent
        {
            Id = id,
            Type = PluginEventType.Custom,
            Name = name,
            Data = data,
            Message = message
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
            s_messageTasks.Clear();
            s_proxys.Clear();
            s_plugins.Clear();
            s_sends.Clear();
            s_sendFilters.Clear();
        }
    }

    /// <summary>
    /// 定向订阅的匹配：只认自定义事件，组件 id 和事件 id 支持 *
    /// </summary>
    /// <param name="hook">订阅</param>
    /// <param name="evt">事件</param>
    private static bool Matches(Hook hook, PluginEvent evt)
    {
        if (hook.SourceId is not { } sourceId)
        {
            return true;
        }

        if (evt.Type != PluginEventType.Custom)
        {
            return false;
        }

        if (sourceId != "*" && !string.Equals(sourceId, evt.Id, StringComparison.Ordinal))
        {
            return false;
        }

        if (hook.EventId == "*")
        {
            return true;
        }

        // 正常事件的名字是 [组件id]事件id，拆不出来就按事件 id 原样比
        return PluginEventName.TryParse(evt.Name, out _, out var eventId)
            ? string.Equals(hook.EventId, eventId, StringComparison.Ordinal)
            : string.Equals(hook.EventId, evt.Name, StringComparison.Ordinal);
    }

    /// <summary>
    /// 订阅，同一个 owner 同一个回调重复订阅只算一次
    /// </summary>
    /// <param name="hooks">目标表</param>
    /// <param name="owner">订阅者</param>
    /// <param name="handler">回调</param>
    /// <param name="sourceId">只收这个组件的事件，null 表示全收</param>
    /// <param name="eventId">只收这个事件，null 表示全收</param>
    private static void Add(List<Hook> hooks, object owner, Delegate handler, string? sourceId = null, string? eventId = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);

        lock (s_lock)
        {
            if (hooks.Contains(new Hook(owner, handler, sourceId, eventId)))
            {
                return;
            }

            hooks.Add(new Hook(owner, handler, sourceId, eventId));
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