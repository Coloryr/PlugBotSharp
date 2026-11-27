using Microsoft.AspNetCore.Http;
using PlugBotSharp.Core.Http;
using PlugBotSharp.Api.Messages;
using PlugBotSharp.Api;
using PlugBotSharp.Api.Events;

namespace PlugBotSharp.Core.Components;

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

        RegisterHttpPathByOwner(_owner, handler);
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
    public StateValueHandle RegisterState(string name, string label = "", string description = "")
    {
        EnsureOpen();

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var handle = new StateValueHandle(_owner, name, label, description, StatePath);

        // 先把名字和描述记下来，同时把上次落盘的值读回来
        StateRegistry.Declare(_owner, name, StateRegistry.ValueKind, handle.Label, handle.Description, handle.StorePath);

        return handle;
    }

    /// <inheritdoc/>
    public StateKvHandle RegisterKvState(string name, string label = "", string description = "")
    {
        EnsureOpen();

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var handle = new StateKvHandle(_owner, name, label, description, StatePath);

        StateRegistry.Declare(_owner, name, StateRegistry.KvKind, handle.Label, handle.Description, handle.StorePath);

        return handle;
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

        // 实例带上 uuid 前缀，调试面板上能区分是哪个实例发的
        var ownerId = _ownUuid is null ? _id : _id + "/" + _ownUuid.Value.ToString()[..8];

        return new SendHandle(_ownProxyId, _ownUuid, ownerId);
    }

    /// <summary>
    /// 按组件对象注册 http 接口，插件 / 代理是 /api/{id}，实例再加 uuid
    /// </summary>
    /// <param name="owner">组件对象</param>
    /// <param name="call">回调</param>
    private static void RegisterHttpPathByOwner(object owner, Func<HttpContext, Task> call)
    {
        switch (owner)
        {
            case IBasePlugin plugin when ComponentManager.FindPluginId(plugin) is { } pluginId:
                Register("api/" + pluginId, owner, call);
                break;
            case IBaseProxy proxy when ComponentManager.FindProxyId(proxy) is { } proxyId:
                Register("api/" + proxyId, owner, call);
                break;
            case IProxyInstasnce instance when InstanceManager.TryGetInstance(instance, out var proxyId, out var uuid):
                Register($"api/{proxyId}/{uuid}", owner, call);
                break;
            default:
                throw new InvalidOperationException(
                    BotLang.GetLang(BotLangKey.ComponentRegisterFail, owner.GetType().FullName));
        }
    }

    /// <summary>
    /// 登记一条 http 路由
    /// </summary>
    /// <param name="path">路径</param>
    /// <param name="owner">组件对象</param>
    /// <param name="call">回调</param>
    private static void Register(string path, object owner, Func<HttpContext, Task> call)
    {
        ApiRegistry.Register(path, new ApiEntry
        {
            Owner = owner,
            Call = call
        });
    }

    /// <summary>
    /// 状态落盘的位置：组件在 Configs/{id}/states.json，实例在 Instances/{uuid}/states.json
    /// </summary>
    private string StatePath => _ownUuid is { } uuid
        ? Path.Combine(BotManager.InstancesPath, uuid.ToString(), "states.json")
        : Path.Combine(BotManager.ConfigsPath, _id, "states.json");

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
