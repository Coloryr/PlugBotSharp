using System.Text.Json.Nodes;
using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.Core;

/// 自定义事件的触发器，注册事件时返回。
/// 事件数据只能是基础类型（字符串 / 布尔 / 数字）、JsonNode 或 BaseMessage，不接受组件自定义的对象：
/// 组件在各自的 AssemblyLoadContext 里，传自定义对象会把它的类型和程序集带到别的组件，卸载时回收不掉
/// </summary>
public sealed class RaiseHandle
{
    internal RaiseHandle(string id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>
    /// 来源组件 id
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 事件全名，[组件id]事件id，订阅时直接用这个
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 触发事件
    /// </summary>
    /// <param name="data">事件数据，JSON 节点，没数据就留空</param>
    public void Raise(JsonNode? data = null)
    {
        EventManager.RaisePlugin(Id, Name, data);
    }

    /// <summary>
    /// 触发事件，带一条消息
    /// </summary>
    /// <param name="message">消息</param>
    public void Raise(BaseMessage message)
    {
        EventManager.RaisePlugin(Id, Name, null, message);
    }

    /// <summary>
    /// 触发事件，数据是字符串
    /// </summary>
    /// <param name="value">字符串</param>
    public void Raise(string value)
    {
        Raise(JsonValue.Create(value));
    }

    /// <summary>
    /// 触发事件，数据是布尔
    /// </summary>
    /// <param name="value">布尔值</param>
    public void Raise(bool value)
    {
        Raise(JsonValue.Create(value));
    }

    /// <summary>
    /// 触发事件，数据是整数
    /// </summary>
    /// <param name="value">整数</param>
    public void Raise(int value)
    {
        Raise(JsonValue.Create(value));
    }

    /// <summary>
    /// 触发事件，数据是长整数
    /// </summary>
    /// <param name="value">长整数</param>
    public void Raise(long value)
    {
        Raise(JsonValue.Create(value));
    }

    /// <summary>
    /// 触发事件，数据是浮点数
    /// </summary>
    /// <param name="value">浮点数</param>
    public void Raise(double value)
    {
        Raise(JsonValue.Create(value));
    }
}
