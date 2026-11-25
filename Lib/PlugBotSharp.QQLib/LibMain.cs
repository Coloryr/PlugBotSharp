using PlugBotSharp.Core;

namespace PlugBotSharp.QQLib;

/// <summary>
/// QQLib 只有数据模型和转换方法，没有运行逻辑
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
