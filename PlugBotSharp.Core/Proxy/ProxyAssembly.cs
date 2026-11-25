using System;
using System.Collections.Generic;
using System.Text;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Http;

namespace PlugBotSharp.Core.Proxy;

internal class ProxyAssembly : BaseAssembly
{
    public IBaseProxy Proxy { get; private set; }

    public ProxyAssembly(IBaseProxy proxy)
    {
        Proxy = proxy;
    }

    /// <summary>
    /// 断开对实例和加载上下文的引用
    /// </summary>
    internal override void Detach()
    {
        Proxy = null!;
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

        if (!Proxy.Init(ConfigPath))
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
        if (State is ComponentState.Loaded or ComponentState.Enabled or ComponentState.Disabled)
        {
            return true;
        }

        if (State != ComponentState.Inited)
        {
            return false;
        }

        if (!Proxy.Load())
        {
            return false;
        }

        State = ComponentState.Loaded;
        Trace(BotLangKey.ComponentLoaded);

        return true;
    }

    /// <summary>
    /// 启用
    /// </summary>
    public bool Enable()
    {
        if (State == ComponentState.Enabled)
        {
            return true;
        }

        if (State is not (ComponentState.Loaded or ComponentState.Disabled))
        {
            return false;
        }

        if (!Proxy.Enable())
        {
            return false;
        }

        State = ComponentState.Enabled;
        StateManager.SetComponentEnabled(Meta.Id, true);

        return true;
    }

    /// <summary>
    /// 禁用
    /// </summary>
    /// <param name="persist">是否把禁用状态写进记录，关停时传 false</param>
    public bool Disable(bool persist = true)
    {
        if (State != ComponentState.Enabled)
        {
            return true;
        }

        try
        {
            return Proxy.Disable();
        }
        finally
        {
            State = ComponentState.Disabled;
            ApiRegistry.Unregister(Proxy);

            if (persist)
            {
                StateManager.SetComponentEnabled(Meta.Id, false);
            }
        }
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
            return Proxy.Unload();
        }
        finally
        {
            State = ComponentState.Unloaded;
            ApiRegistry.Unregister(Proxy);
        }
    }

    /// <summary>
    /// 创建实例
    /// </summary>
    public IProxyInstasnce CreateInstance()
    {
        return Proxy.CreateInstance();
    }

    /// <summary>
    /// 重载组件配置
    /// </summary>
    public void Reload()
    {
        Proxy.Reload();
    }

    /// <summary>
    /// 重载实例配置
    /// </summary>
    public void Reload(IProxyInstasnce instance)
    {
        Proxy.Reload(instance);
    }

    /// <summary>
    /// 读取组件配置项，交给网页端渲染
    /// </summary>
    public Dictionary<string, ConfigMap> ConfigMap()
    {
        return Proxy.ConfigMap();
    }

    /// <summary>
    /// 保存网页端提交的配置项
    /// </summary>
    public void UpdateMap(Dictionary<string, ConfigMap> data)
    {
        Proxy.UpdateMap(data);
    }
}
