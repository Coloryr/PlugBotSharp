using PlugBotSharp.Core;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.Core.Proxy;

namespace PlugBotSharp.Tests;

/// <summary>
/// 测试用代理组件
/// </summary>
internal sealed class FakeProxy : IBaseProxy
{
    public int EnableCount { get; private set; }
    public int DisableCount { get; private set; }
    public int ReloadCount { get; private set; }
    public int InstanceReloadCount { get; private set; }

    /// <summary>
    /// 注册阶段拿到的注册器
    /// </summary>
    public IRegister? Register { get; private set; }

    public void RegisterEventHook(IRegister register)
    {
        Register = register;
    }

    public bool Init(string path) => true;

    public bool Load() => true;

    public bool Unload() => true;

    /// <summary>
    /// 最近一次创建的实例，测试里用来拿实例对象
    /// </summary>
    public FakeInstance? LastInstance { get; private set; }

    public IProxyInstasnce CreateInstance()
    {
        LastInstance = new FakeInstance();

        return LastInstance;
    }

    public void Reload()
    {
        ReloadCount++;
    }

    public void Reload(IProxyInstasnce instasnce)
    {
        InstanceReloadCount++;
    }

    /// <summary>
    /// 最近一次提交的配置项
    /// </summary>
    public Dictionary<string, ConfigMap>? LastUpdate { get; private set; }

    public Dictionary<string, ConfigMap> ConfigMap()
    {
        return new()
        {
            ["Log"] = new ConfigBoolMap { Key = "Log", Value = false, Default = false }
        };
    }

    public void UpdateMap(Dictionary<string, ConfigMap> data)
    {
        LastUpdate = data;
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
    public int DisableCount { get; private set; }
    public int ReloadCount { get; private set; }
    public int HookCount { get; private set; }

    public void Init(string path, Guid uuid)
    {
    }

    public bool Load() => true;

    public void Reload()
    {
        ReloadCount++;
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

    /// <summary>
    /// 注册阶段拿到的注册器
    /// </summary>
    public IRegister? Register { get; private set; }

    public void RegisterEventHook(IRegister register)
    {
        HookCount++;
        Register = register;
    }

    public bool Unload() => true;

    public void Tick()
    {
    }

    /// <summary>
    /// 最近一次收到的发送请求
    /// </summary>
    public ProxyMessage? LastSend { get; private set; }

    /// <summary>
    /// 最近一次要发的消息
    /// </summary>
    public BaseMessage? LastReply { get; private set; }

    public Task SendAsync(ProxyMessage message, BaseMessage reply)
    {
        LastSend = message;
        LastReply = reply;

        return Task.CompletedTask;
    }

    /// <summary>
    /// 实例名称，作为可配置项用来测接口
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 最近一次提交的配置项
    /// </summary>
    public Dictionary<string, ConfigMap>? LastUpdate { get; private set; }

    public Dictionary<string, ConfigMap> ConfigMap()
    {
        return new()
        {
            ["Name"] = new ConfigStringMap { Key = "Name", Value = Name, Default = "fake" }
        };
    }

    public void UpdateMap(Dictionary<string, ConfigMap> data)
    {
        LastUpdate = data;

        if (data.TryGetValue("Name", out var item) && item is ConfigStringMap map)
        {
            Name = map.Value ?? "";
        }
    }
}