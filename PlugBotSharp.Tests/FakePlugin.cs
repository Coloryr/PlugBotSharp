using PlugBotSharp.Core.Plugin;

namespace PlugBotSharp.Tests;

/// <summary>
/// 记录生命周期调用次数的测试插件
/// </summary>
internal sealed class FakePlugin : IBasePlugin
{
    public static readonly List<string> UnloadOrder = [];

    public string Name { get; init; } = "";

    public int InitCount { get; private set; }
    public int LoadCount { get; private set; }
    public int EnableCount { get; private set; }
    public int DisableCount { get; private set; }
    public int UnloadCount { get; private set; }

    public bool InitResult { get; set; } = true;
    public bool LoadResult { get; set; } = true;
    public bool EnableResult { get; set; } = true;

    public bool Init(string path)
    {
        InitCount++;
        return InitResult;
    }

    public bool Load()
    {
        LoadCount++;
        return LoadResult;
    }

    public bool Enable()
    {
        EnableCount++;
        return EnableResult;
    }

    public bool Disable()
    {
        DisableCount++;
        return true;
    }

    public bool Unload()
    {
        UnloadCount++;
        UnloadOrder.Add(Name);
        return true;
    }
}
