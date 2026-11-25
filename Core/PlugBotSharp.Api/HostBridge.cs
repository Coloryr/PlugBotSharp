using Microsoft.AspNetCore.Builder;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.Core;

/// <summary>
/// 宿主能力：Core 实现，Api 里的句柄和 ComponentApi 通过它调到宿主；
/// 组件编译期只认 Api，运行期靠宿主把实现注入进来
/// </summary>
public interface IApiHost
{
    /// <summary>触发自定义事件</summary>
    void RaisePlugin(string id, string name, object? data, BaseMessage? message);

    /// <summary>把消息交给所有订阅者，返回要回复的消息</summary>
    List<BaseMessage> Dispatch(ProxyMessage message);

    /// <summary>把消息交给目标实例发出去</summary>
    Task SendAsync(ProxyMessage context, BaseMessage message, string ownerId);

    /// <summary>声明状态</summary>
    void DeclareState(object owner, string name, string kind, string label, string description, string storePath);

    /// <summary>设置单值状态</summary>
    void SetStateValue(object owner, string name, object? value);

    /// <summary>设置键值状态的一个键</summary>
    void SetStateKey(object owner, string name, string key, object? value);

    /// <summary>删掉键值状态的一个键</summary>
    void RemoveStateKey(object owner, string name, string key);

    /// <summary>清空键值状态</summary>
    void ClearStateKeys(object owner, string name);

    /// <summary>取状态当前的值</summary>
    object? GetStateValue(object owner, string name);

    /// <summary>移除组件的全部状态</summary>
    void RemoveState(object owner);

    /// <summary>移除单个状态</summary>
    void RemoveState(object owner, string name);

    /// <summary>全部组件</summary>
    List<ComponentInfo> GetComponents();

    /// <summary>全部实例</summary>
    List<InstanceInfo> GetInstances();

    /// <summary>组件状态</summary>
    string GetComponentState(string id);

    /// <summary>启动组件</summary>
    bool StartComponent(string id);

    /// <summary>禁用组件</summary>
    bool DisableComponent(string id);

    /// <summary>重启组件</summary>
    bool ReloadComponent(string id);

    /// <summary>组件配置</summary>
    Dictionary<string, ConfigMap>? GetComponentConfig(string id);

    /// <summary>更新组件配置</summary>
    bool UpdateComponentConfig(string id, Dictionary<string, ConfigMap> map);

    /// <summary>启用实例</summary>
    bool EnableInstance(Guid uuid);

    /// <summary>禁用实例</summary>
    bool DisableInstance(Guid uuid);

    /// <summary>重启实例</summary>
    bool ReloadInstance(Guid uuid);

    /// <summary>实例配置</summary>
    Dictionary<string, ConfigMap>? GetInstanceConfig(Guid uuid);

    /// <summary>更新实例配置</summary>
    bool UpdateInstanceConfig(Guid uuid, Dictionary<string, ConfigMap> map);

    /// <summary>取消注册 http 接口</summary>
    bool UnregisterHttpPath(object owner);

    /// <summary>登记一个组件自建的服务器</summary>
    void AddServer(object owner, ComponentServer server);

    /// <summary>宿主当前的语言</summary>
    Lang Lang { get; }

    /// <summary>记错误日志</summary>
    void LogError(Exception? exception, string message);

    /// <summary>记警告日志</summary>
    void LogWarn(string message);

    /// <summary>记普通日志</summary>
    void LogInfo(string message);
}

/// <summary>
/// 宿主实现的存放处，Core 启动时注入
/// </summary>
public static class ApiHost
{
    /// <summary>
    /// 当前宿主的实现
    /// </summary>
    public static IApiHost? Current { get; set; }

    /// <summary>
    /// 取宿主实现，没初始化时抛异常
    /// </summary>
    public static IApiHost Host => Current ?? throw new InvalidOperationException("宿主还没初始化");
}
