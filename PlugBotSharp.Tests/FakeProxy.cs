using PlugBotSharp.Core.Proxy;

namespace PlugBotSharp.Tests;

/// <summary>
/// 测试用代理组件
/// </summary>
internal sealed class FakeProxy : IBaseProxy
{
    public int EnableCount { get; private set; }
    public int DisableCount { get; private set; }

    public bool Init(string path) => true;

    public bool Load() => true;

    public bool Unload() => true;

    public IProxyInstasnce CreateInstance() => new FakeInstance();

    public void ConfigInstance(IProxyInstasnce instasnce)
    {
    }

    public bool Enable()
    {
        EnableCount++;
        return true;
    }

    public bool Disable()
    {
        DisableCount++;
        return true;
    }
}

/// <summary>
/// 测试用通信实例
/// </summary>
internal sealed class FakeInstance : IProxyInstasnce
{
    public int EnableCount { get; private set; }

    public void Init(string path, Guid uuid)
    {
    }

    public Task<bool> Load() => Task.FromResult(true);

    public Task<bool> Enable()
    {
        EnableCount++;
        return Task.FromResult(true);
    }

    public Task<bool> Disable() => Task.FromResult(true);

    public void Tick()
    {
    }
}
