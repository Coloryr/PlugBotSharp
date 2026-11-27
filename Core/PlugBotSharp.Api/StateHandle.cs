namespace PlugBotSharp.Api;

/// <summary>
/// 状态句柄的公共部分：注册状态时拿到，之后随时更新。
/// 单值状态用 RegisterState，只读键值状态用 RegisterKvState，两种都在面板上展示，面板侧不能改
/// </summary>
public abstract class StateHandle
{
    /// <summary>
    /// 创建句柄
    /// </summary>
    /// <param name="owner">上报状态的组件对象</param>
    /// <param name="name">状态名，面板接口里当键用</param>
    /// <param name="label">面板上显示的名字，空时用状态名</param>
    /// <param name="description">面板上的解释</param>
    /// <param name="storePath">落盘路径，宿主会自动把值存到这里</param>
    internal StateHandle(object owner, string name, string label, string description, string storePath)
    {
        Owner = owner;
        Name = name;
        Label = string.IsNullOrWhiteSpace(label) ? name : label;
        Description = description ?? "";
        StorePath = storePath;
    }

    /// <summary>
    /// 落盘路径，宿主自动保存用
    /// </summary>
    internal string StorePath { get; }

    /// <summary>
    /// 状态名，也是面板接口里的键
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 面板上显示的名字，注册时没填就和 <see cref="Name"/> 一样
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// 面板上显示的解释，鼠标停在标签上能看到
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 上报状态的组件对象
    /// </summary>
    internal object Owner { get; }

    /// <summary>
    /// 移除这个状态
    /// </summary>
    public void Remove()
    {
        ApiHost.Host.RemoveState(Owner, Name);
    }
}
