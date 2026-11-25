using PlugBotSharp.Core;

namespace DemoLib;

/// <summary>
/// 示例运行库：运行库只负责提供类型，不参与启用 / 禁用
/// </summary>
public class LibMain : IBaseLib
{
    /// <inheritdoc/>
    public bool Init(string path)
    {
        return true;
    }

    /// <inheritdoc/>
    public bool Load()
    {
        return true;
    }

    /// <inheritdoc/>
    public bool Unload()
    {
        return true;
    }
}
