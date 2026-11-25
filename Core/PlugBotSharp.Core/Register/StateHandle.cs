using PlugBotSharp.Core.Http;

namespace PlugBotSharp.Core;

/// <summary>
/// 自定义状态的句柄，注册状态时返回，之后随时更新
/// </summary>
public sealed class StateHandle
{
    private readonly object _owner;

    internal StateHandle(object owner, string name)
    {
        _owner = owner;
        Name = name;
    }

    /// <summary>
    /// 状态名，面板上显示成标签
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 更新状态值
    /// </summary>
    /// <param name="value">状态值</param>
    public void Set(string value)
    {
        StateRegistry.Set(_owner, Name, value);
    }

    /// <summary>
    /// 移除这个状态
    /// </summary>
    public void Remove()
    {
        StateRegistry.Remove(_owner, Name);
    }
}
