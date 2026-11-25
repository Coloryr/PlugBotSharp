using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core.Lib;

internal class LibAssembly : BaseAssembly
{
    public IBaseLib Lib { get; private set; }

    public LibAssembly(IBaseLib lib)
    {
        Lib = lib;
    }

    /// <summary>
    /// 断开对实例和加载上下文的引用
    /// </summary>
    internal override void Detach()
    {
        Lib = null!;
        base.Detach();
    }

    /// <summary>
    /// 初始化，只调用一次
    /// </summary>
    public bool Init()
    {
        if (State != ComponentState.None)
        {
            return true;
        }

        if (!Lib.Init(ConfigPath))
        {
            return false;
        }

        State = ComponentState.Inited;
        Trace(BotLangKey.ComponentInited);

        return true;
    }

    /// <summary>
    /// 加载
    /// </summary>
    public bool Load()
    {
        if (State == ComponentState.Loaded)
        {
            return true;
        }

        if (State != ComponentState.Inited)
        {
            return false;
        }

        if (!Lib.Load())
        {
            return false;
        }

        State = ComponentState.Loaded;
        Trace(BotLangKey.ComponentLoaded);

        return true;
    }

    /// <summary>
    /// 卸载
    /// </summary>
    public bool Unload()
    {
        if (State is ComponentState.Unloaded or ComponentState.None)
        {
            return true;
        }

        try
        {
            return Lib.Unload();
        }
        finally
        {
            State = ComponentState.Unloaded;
        }
    }
}
