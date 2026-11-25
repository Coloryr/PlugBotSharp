using PlugBotSharp.Core.Http;

namespace PlugBotSharp.Core;

/// <summary>
/// 只读键值状态的句柄：一个名字下面挂一组 键 -&gt; 值，面板上显示成可展开的表格。
/// 只读指的是面板侧不能改，插件 / 代理这边随时能增删
/// </summary>
public sealed class StateKvHandle : StateHandle
{
    /// <summary>
    /// 创建句柄
    /// </summary>
    /// <param name="owner">上报状态的组件对象</param>
    /// <param name="name">状态名，面板接口里当键用</param>
    /// <param name="label">面板上显示的名字，空时用状态名</param>
    /// <param name="description">面板上的解释</param>
    internal StateKvHandle(object owner, string name, string label, string description)
        : base(owner, name, label, description)
    {
    }

    /// <summary>
    /// 设置一个键，值是任意基础类型
    /// </summary>
    /// <param name="key">键，同一个状态内唯一</param>
    /// <param name="value">值，基础类型或 null</param>
    public void Set(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        StateRegistry.SetKey(Owner, Name, key, value);
    }

    /// <summary>
    /// 删掉一个键
    /// </summary>
    /// <param name="key">键</param>
    public void Remove(string key)
    {
        StateRegistry.RemoveKey(Owner, Name, key);
    }

    /// <summary>
    /// 清空全部键，状态本身留着，面板上显示成 0 项
    /// </summary>
    public void Clear()
    {
        StateRegistry.ClearKeys(Owner, Name);
    }
}