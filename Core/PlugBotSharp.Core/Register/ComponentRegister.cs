using Microsoft.AspNetCore.Http;
using PlugBotSharp.Core.Http;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.Core.Proxy;

namespace PlugBotSharp.Core;

/// <summary>
/// 注册器的实现，Registration 阶段结束就关闭，之后再用直接抛异常
/// </summary>
internal sealed class ComponentRegister : IRegister
{
    private readonly object _owner;
    private readonly string _id;
    private readonly string? _ownProxyId;
    private readonly Guid? _ownUuid;

    private bool _open = true;

    /// <summary>
    /// 创建注册器
    /// </summary>
    /// <param name="owner">组件 / 实例对象</param>
    /// <param name="id">组件 id</param>
    /// <param name="ownProxyId">代理自己的 id，插件为 null</param>
    /// <param name="ownUuid">实例自己的 uuid，组件为 null</param>
    public ComponentRegister(object owner, string id, string? ownProxyId = null, Guid? ownUuid = null)
    {
        _owner = owner;
        _id = id;
        _ownProxyId = ownProxyId;
        _ownUuid = ownUuid;
    }

    /// <summary>
    /// 结束注册阶段
    /// </summary>
    public void Close()
    {
        _open = false;
    }

    /// <inheritdoc/>
    public RaiseHandle RegisterPluginEvent(string eventId)
    {
        EnsureOpen();

        return new RaiseHandle(_id, PluginEventName.Build(_id, eventId));
    }

    /// <inheritdoc/>
    public void RegisterHttpPath(Func<HttpContext, Task> handler)
    {
        EnsureOpen();

        ComponentApi.RegisterHttpPathByOwner(_owner, handler);
    }

    /// <inheritdoc/>
    public void SubscribeMessage(Func<ProxyMessage, List<BaseMessage>> handler)
    {
        EnsureOpen();

        EventManager.SubscribeMessage(_owner, handler);
    }

    /// <inheritdoc/>
    public void SubscribeMessage(Func<MessageEvent, Task> handler)
    {
        EnsureOpen();

        EventManager.SubscribeMessage(_owner, handler);
    }

    /// <inheritdoc/>
    public void SubscribeProxy(Action<ProxyEvent> handler)
    {
        EnsureOpen();

        EventManager.SubscribeProxy(_owner, handler);
    }

    /// <inheritdoc/>
    public void SubscribePlugin(Action<PluginEvent> handler)
    {
        EnsureOpen();

        EventManager.SubscribePlugin(_owner, handler);
    }

    /// <inheritdoc/>
    public void SubscribePluginEvent(string name, Action<PluginEvent> handler)
    {
        EnsureOpen();

        if (!PluginEventName.TryParse(name, out var componentId, out var eventId))
        {
            throw new ArgumentException(BotLang.GetLang(BotLangKey.EventNameInvalid, name ?? ""), nameof(name));
        }

        EventManager.SubscribePluginEvent(_owner, componentId, eventId, handler);
    }

    /// <inheritdoc/>
    public void SubscribePluginEvent(string componentId, string eventId, Action<PluginEvent> handler)
    {
        EnsureOpen();

        if (!IsPattern(componentId) || !IsPattern(eventId))
        {
            throw new ArgumentException(
                BotLang.GetLang(BotLangKey.EventPatternInvalid, componentId ?? "", eventId ?? ""), nameof(componentId));
        }

        EventManager.SubscribePluginEvent(_owner, componentId, eventId, handler);
    }

    /// <summary>
    /// 组件 id / 事件 id 要么是具体名字，要么是 *
    /// </summary>
    /// <param name="value">值</param>
    private static bool IsPattern(string? value)
    {
        return value == "*" || (!string.IsNullOrWhiteSpace(value) && !value.Contains('[') && !value.Contains(']'));
    }

    /// <inheritdoc/>
    public void SubscribeSendFilter(Func<ProxyMessage, BaseMessage, Task<bool>> handler)
    {
        EnsureOpen();

        EventManager.SubscribeSendFilter(_owner, handler);
    }

    /// <inheritdoc/>
    public StateHandle RegisterState(string name)
    {
        EnsureOpen();

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new StateHandle(_owner, name);
    }

    /// <inheritdoc/>
    public ProxyEventHandle RegisterProxyEvent()
    {
        EnsureOpen();

        if (_owner is not (IBaseProxy or IProxyInstasnce))
        {
            throw new InvalidOperationException(BotLang.GetLang(BotLangKey.ProxyEventDenied, _id));
        }

        return new ProxyEventHandle();
    }

    /// <inheritdoc/>
    public SendHandle RegisterSend()
    {
        EnsureOpen();

        return new SendHandle(_ownProxyId, _ownUuid);
    }

    /// <summary>
    /// 注册阶段结束后不允许再注册
    /// </summary>
    private void EnsureOpen()
    {
        if (!_open)
        {
            throw new InvalidOperationException(BotLang.GetLang(BotLangKey.RegisterClosed, _id));
        }
    }
}
