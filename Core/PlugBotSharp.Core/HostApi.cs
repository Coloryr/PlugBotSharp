using PlugBotSharp.Api;
using PlugBotSharp.Api.Configs;
using PlugBotSharp.Api.Messages;
using PlugBotSharp.Core.Components;
using PlugBotSharp.Core.Http;

namespace PlugBotSharp.Core;

/// <summary>
/// 宿主侧实现的 API，把 <see cref="IApiHost"/> 转到内部的管理器上
/// </summary>
internal sealed class HostApi : IApiHost
{
    /// <inheritdoc/>
    public void RaisePlugin(string id, string name, object? data, BaseMessage? message)
    {
        EventManager.RaisePlugin(id, name, data, message);
    }

    /// <inheritdoc/>
    public List<BaseMessage> Dispatch(ProxyMessage message)
    {
        return EventManager.HandleMessage(message);
    }

    /// <inheritdoc/>
    public Task SendAsync(ProxyMessage context, BaseMessage message, string ownerId)
    {
        return EventManager.HandleSendAsync(context, message, ownerId);
    }

    /// <inheritdoc/>
    public void DeclareState(object owner, string name, string kind, string label, string description, string storePath)
    {
        StateRegistry.Declare(owner, name, kind, label, description, storePath);
    }

    /// <inheritdoc/>
    public void SetStateValue(object owner, string name, object? value)
    {
        StateRegistry.SetValue(owner, name, value);
    }

    /// <inheritdoc/>
    public void SetStateKey(object owner, string name, string key, object? value)
    {
        StateRegistry.SetKey(owner, name, key, value);
    }

    /// <inheritdoc/>
    public void RemoveStateKey(object owner, string name, string key)
    {
        StateRegistry.RemoveKey(owner, name, key);
    }

    /// <inheritdoc/>
    public void ClearStateKeys(object owner, string name)
    {
        StateRegistry.ClearKeys(owner, name);
    }

    /// <inheritdoc/>
    public object? GetStateValue(object owner, string name)
    {
        return StateRegistry.GetValue(owner, name);
    }

    /// <inheritdoc/>
    public void RemoveState(object owner)
    {
        StateRegistry.Remove(owner);
    }

    /// <inheritdoc/>
    public void RemoveState(object owner, string name)
    {
        StateRegistry.Remove(owner, name);
    }

    /// <inheritdoc/>
    public List<ComponentInfo> GetComponents()
    {
        return ComponentManager.ListAll().Select(x => new ComponentInfo
        {
            Id = x.Meta.Id,
            Name = x.Meta.Name ?? "",
            Version = x.Meta.Version ?? "",
            Type = x.Meta.Type,
            Description = x.Meta.Description ?? "",
            Folder = x.Folder,
            Loaded = x.Assembly is not null,
            Enabled = x.Assembly?.State == ComponentState.Enabled,
            Blocked = ComponentManager.IsComponentBlocked(x.Meta.Id),
            State = ComponentManager.GetState(x.Meta.Id)
        }).ToList();
    }

    /// <inheritdoc/>
    public List<InstanceInfo> GetInstances()
    {
        return InstanceManager.List();
    }

    /// <inheritdoc/>
    public string GetComponentState(string id)
    {
        return ComponentManager.GetState(id);
    }

    /// <inheritdoc/>
    public bool StartComponent(string id)
    {
        var assembly = ComponentManager.Find(id);

        if (assembly is null)
        {
            if (!ComponentLoader.LoadComponent(id))
            {
                return false;
            }

            assembly = ComponentManager.Find(id);
        }

        if (assembly is null)
        {
            return false;
        }

        return assembly.Meta.Type == ComponentType.Lib || ComponentManager.EnableComponent(id);
    }

    /// <inheritdoc/>
    public bool DisableComponent(string id)
    {
        return ComponentManager.DisableComponent(id);
    }

    /// <inheritdoc/>
    public bool ReloadComponent(string id)
    {
        return ComponentManager.ReloadComponent(id);
    }

    /// <inheritdoc/>
    public Dictionary<string, ConfigMap>? GetComponentConfig(string id)
    {
        return ComponentManager.TryGetConfigMap(id, out var map) ? map : null;
    }

    /// <inheritdoc/>
    public bool UpdateComponentConfig(string id, Dictionary<string, ConfigMap> map)
    {
        return ComponentManager.TryUpdateConfigMap(id, map);
    }

    /// <inheritdoc/>
    public bool EnableInstance(Guid uuid)
    {
        if (!InstanceManager.TryLoad(uuid, out var error))
        {
            BotManager.Logger?.Warn(error);

            return false;
        }

        if (InstanceManager.List().Any(x => x.Uuid == uuid && x.Enabled))
        {
            return true;
        }

        var ok = InstanceManager.TryEnable(uuid, out error);

        if (!ok)
        {
            BotManager.Logger?.Warn(error);
        }

        return ok;
    }

    /// <inheritdoc/>
    public bool DisableInstance(Guid uuid)
    {
        var ok = InstanceManager.TryDisable(uuid, out var error);

        if (!ok)
        {
            BotManager.Logger?.Warn(error);
        }

        return ok;
    }

    /// <inheritdoc/>
    public bool ReloadInstance(Guid uuid)
    {
        if (!InstanceManager.TryLoad(uuid, out var error))
        {
            BotManager.Logger?.Warn(error);

            return false;
        }

        var ok = InstanceManager.TryReload(uuid, out error);

        if (!ok)
        {
            BotManager.Logger?.Warn(error);
        }

        return ok;
    }

    /// <inheritdoc/>
    public Dictionary<string, ConfigMap>? GetInstanceConfig(Guid uuid)
    {
        return InstanceManager.TryGetConfigMap(uuid, out var map) ? map : null;
    }

    /// <inheritdoc/>
    public bool UpdateInstanceConfig(Guid uuid, Dictionary<string, ConfigMap> map)
    {
        return InstanceManager.TryUpdateConfigMap(uuid, map);
    }

    /// <inheritdoc/>
    public bool UnregisterHttpPath(object owner)
    {
        return owner switch
        {
            IBasePlugin plugin => ComponentManager.FindPluginId(plugin) is { } id
                ? ApiRegistry.Unregister("api/" + id, plugin)
                : ApiRegistry.Unregister(plugin),
            IBaseProxy proxy => ComponentManager.FindProxyId(proxy) is { } id
                ? ApiRegistry.Unregister("api/" + id, proxy)
                : ApiRegistry.Unregister(proxy),
            IProxyInstasnce instance => InstanceManager.TryGetInstance(instance, out var proxyId, out var uuid)
                ? ApiRegistry.Unregister($"api/{proxyId}/{uuid}", instance)
                : ApiRegistry.Unregister(instance),
            _ => false
        };
    }

    /// <inheritdoc/>
    public void AddServer(object owner, ComponentServer server)
    {
        ServerRegistry.Add(owner, server);
    }

    /// <inheritdoc/>
    public Lang Lang => BotManager.Lang;

    /// <inheritdoc/>
    public void LogError(Exception? exception, string message)
    {
        BotManager.Logger?.Error(exception, message);
    }

    /// <inheritdoc/>
    public void LogWarn(string message)
    {
        BotManager.Logger?.Warn(message);
    }

    /// <inheritdoc/>
    public void LogInfo(string message)
    {
        BotManager.Logger?.Info(message);
    }
}
