using System.Text.Json.Nodes;
using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.Core;

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
    /// 事件数据，JSON 节点：跨 ALC 只传共享类型，不传组件自己的对象
    /// </summary>
    public JsonNode? Data { get; init; }
    /// <summary>
    /// 事件带的消息，和 Data 二选一
    /// </summary>
    public BaseMessage? Message { get; init; }
}
