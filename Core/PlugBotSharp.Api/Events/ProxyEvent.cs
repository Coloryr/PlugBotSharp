namespace PlugBotSharp.Api.Events;

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
