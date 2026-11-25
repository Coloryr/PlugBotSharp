using System.Text.Json.Nodes;
using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.Core;

/// <summary>
/// 自定义事件的触发器，注册事件时返回。
/// 事件数据只能是基础类型（字符串 / 布尔 / 各种整数和小数）、JsonNode 或 BaseMessage，不接受组件自定义的对象：
/// 组件在各自的 AssemblyLoadContext 里，传自定义对象会把它的类型和程序集带到别的组件，卸载时回收不掉。
/// 数据按原类型传下去，订阅方用 data is string / data is int 就能判断
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
    /// 触发事件，数据是结构化的 JSON
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
        EventManager.RaisePlugin(Id, Name, value);
    }

    /// <summary>
    /// 触发事件，数据是字符
    /// </summary>
    /// <param name="value">字符</param>
    public void Raise(char value)
    {
        EventManager.RaisePlugin(Id, Name, value);
    }

    /// <summary>
    /// 触发事件，数据是布尔
    /// </summary>
    /// <param name="value">布尔值</param>
    public void Raise(bool value)
    {
        EventManager.RaisePlugin(Id, Name, value);
    }

    /// <summary>
    /// 触发事件，数据是字节
    /// </summary>
    /// <param name="value">字节</param>
    public void Raise(byte value)
    {
        EventManager.RaisePlugin(Id, Name, value);
    }

    /// <summary>
    /// 触发事件，数据是有符号字节
    /// </summary>
    /// <param name="value">有符号字节</param>
    public void Raise(sbyte value)
    {
        EventManager.RaisePlugin(Id, Name, value);
    }

    /// <summary>
    /// 触发事件，数据是短整数
    /// </summary>
    /// <param name="value">短整数</param>
    public void Raise(short value)
    {
        EventManager.RaisePlugin(Id, Name, value);
    }

    /// <summary>
    /// 触发事件，数据是无符号短整数
    /// </summary>
    /// <param name="value">无符号短整数</param>
    public void Raise(ushort value)
    {
        EventManager.RaisePlugin(Id, Name, value);
    }

    /// <summary>
    /// 触发事件，数据是整数
    /// </summary>
    /// <param name="value">整数</param>
    public void Raise(int value)
    {
        EventManager.RaisePlugin(Id, Name, value);
    }

    /// <summary>
    /// 触发事件，数据是无符号整数
    /// </summary>
    /// <param name="value">无符号整数</param>
    public void Raise(uint value)
    {
        EventManager.RaisePlugin(Id, Name, value);
    }

    /// <summary>
    /// 触发事件，数据是长整数
    /// </summary>
    /// <param name="value">长整数</param>
    public void Raise(long value)
    {
        EventManager.RaisePlugin(Id, Name, value);
    }

    /// <summary>
    /// 触发事件，数据是无符号长整数
    /// </summary>
    /// <param name="value">无符号长整数</param>
    public void Raise(ulong value)
    {
        EventManager.RaisePlugin(Id, Name, value);
    }

    /// <summary>
    /// 触发事件，数据是单精度小数
    /// </summary>
    /// <param name="value">单精度小数</param>
    public void Raise(float value)
    {
        EventManager.RaisePlugin(Id, Name, value);
    }

    /// <summary>
    /// 触发事件，数据是双精度小数
    /// </summary>
    /// <param name="value">双精度小数</param>
    public void Raise(double value)
    {
        EventManager.RaisePlugin(Id, Name, value);
    }

    /// <summary>
    /// 触发事件，数据是高精度小数
    /// </summary>
    /// <param name="value">高精度小数</param>
    public void Raise(decimal value)
    {
        EventManager.RaisePlugin(Id, Name, value);
    }
}
