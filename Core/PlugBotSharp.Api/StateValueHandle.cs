namespace PlugBotSharp.Api;

/// <summary>
/// 单值状态的句柄：一个名字一个值，值可以是任意基础类型，面板上显示成 名字: 值
/// </summary>
public sealed class StateValueHandle : StateHandle
{
    /// <summary>
    /// 创建句柄
    /// </summary>
    /// <param name="owner">上报状态的组件对象</param>
    /// <param name="name">状态名，面板接口里当键用</param>
    /// <param name="label">面板上显示的名字，空时用状态名</param>
    /// <param name="description">面板上的解释</param>
    /// <param name="storePath">落盘路径</param>
    internal StateValueHandle(object owner, string name, string label, string description, string storePath)
        : base(owner, name, label, description, storePath)
    {
    }

    /// <summary>
    /// 当前值，重启后是上次落盘的值
    /// </summary>
    public object? Get()
    {
        return ApiHost.Host.GetStateValue(Owner, Name);
    }

    /// <summary>
    /// 更新状态值
    /// </summary>
    /// <param name="value">文本</param>
    public void Set(string value) => ApiHost.Host.SetStateValue(Owner, Name, value);

    /// <summary>
    /// 更新状态值
    /// </summary>
    /// <param name="value">字符</param>
    public void Set(char value) => ApiHost.Host.SetStateValue(Owner, Name, value);

    /// <summary>
    /// 更新状态值
    /// </summary>
    /// <param name="value">开关</param>
    public void Set(bool value) => ApiHost.Host.SetStateValue(Owner, Name, value);

    /// <summary>
    /// 更新状态值
    /// </summary>
    /// <param name="value">字节</param>
    public void Set(byte value) => ApiHost.Host.SetStateValue(Owner, Name, value);

    /// <summary>
    /// 更新状态值
    /// </summary>
    /// <param name="value">有符号字节</param>
    public void Set(sbyte value) => ApiHost.Host.SetStateValue(Owner, Name, value);

    /// <summary>
    /// 更新状态值
    /// </summary>
    /// <param name="value">短整数</param>
    public void Set(short value) => ApiHost.Host.SetStateValue(Owner, Name, value);

    /// <summary>
    /// 更新状态值
    /// </summary>
    /// <param name="value">无符号短整数</param>
    public void Set(ushort value) => ApiHost.Host.SetStateValue(Owner, Name, value);

    /// <summary>
    /// 更新状态值
    /// </summary>
    /// <param name="value">整数</param>
    public void Set(int value) => ApiHost.Host.SetStateValue(Owner, Name, value);

    /// <summary>
    /// 更新状态值
    /// </summary>
    /// <param name="value">无符号整数</param>
    public void Set(uint value) => ApiHost.Host.SetStateValue(Owner, Name, value);

    /// <summary>
    /// 更新状态值
    /// </summary>
    /// <param name="value">长整数</param>
    public void Set(long value) => ApiHost.Host.SetStateValue(Owner, Name, value);

    /// <summary>
    /// 更新状态值
    /// </summary>
    /// <param name="value">无符号长整数</param>
    public void Set(ulong value) => ApiHost.Host.SetStateValue(Owner, Name, value);

    /// <summary>
    /// 更新状态值
    /// </summary>
    /// <param name="value">单精度小数</param>
    public void Set(float value) => ApiHost.Host.SetStateValue(Owner, Name, value);

    /// <summary>
    /// 更新状态值
    /// </summary>
    /// <param name="value">双精度小数</param>
    public void Set(double value) => ApiHost.Host.SetStateValue(Owner, Name, value);

    /// <summary>
    /// 更新状态值
    /// </summary>
    /// <param name="value">高精度小数</param>
    public void Set(decimal value) => ApiHost.Host.SetStateValue(Owner, Name, value);
}