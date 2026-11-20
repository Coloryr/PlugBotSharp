using PlugBotSharp.Core.Plugin;

namespace PlugBotSharp.Core.Lib;

public class CorePlugin : IBaseLib
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
