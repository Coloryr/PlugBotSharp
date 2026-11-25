using System;
using System.Collections.Generic;
using System.Text;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Http;

namespace PlugBotSharp.Core.Plugin;

internal class PluginAssembly : BaseAssembly
{
    public IBasePlugin Plugin { get; private set; }

    // public readonly List<>
    public readonly List<PluginAssembly> Deps = [];

    public PluginAssembly(IBasePlugin plugin)
    {
        Plugin = plugin;
    }

    /// <summary>
    /// 断开对实例和加载上下文的引用
    /// </summary>
    internal override void Detach()
    {
        Plugin = null!;
        Deps.Clear();
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

        if (!Plugin.Init(ConfigPath))
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

        if (!Plugin.Load())
        {
            return false;
        }

        State = ComponentState.Loaded;
        Trace(BotLangKey.ComponentLoaded);
        RaiseEvent(PluginEventType.Loaded);

        try
        {
            Plugin.RegisterEventHook();
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.ComponentActionFail, Meta.Id));
        }

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

        if (!Plugin.Enable())
        {
            return false;
        }

        State = ComponentState.Enabled;
        StateManager.SetComponentEnabled(Meta.Id, true);
        RaiseEvent(PluginEventType.Enabled);

        return true;
    }

    /// <summary>
    /// 重载配置
    /// </summary>
    public void Reload()
    {
        Plugin.Reload();
    }

    /// <summary>
    /// 禁用，并移除插件注册的 http 接口
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
            return Plugin.Disable();
        }
        finally
        {
            State = ComponentState.Disabled;
            ApiRegistry.Unregister(Plugin);
            RaiseEvent(PluginEventType.Disabled);

            if (persist)
            {
                StateManager.SetComponentEnabled(Meta.Id, false);
            }
        }
    }

    /// <summary>
    /// 广播插件生命周期事件
    /// </summary>
    /// <param name="type">事件类型</param>
    private void RaiseEvent(PluginEventType type)
    {
        EventManager.HandlePlugin(new PluginEvent
        {
            Id = Meta.Id,
            Type = type
        });
    }

    /// <summary>
    /// 卸载，并移除插件注册的 http 接口
    /// </summary>
    public bool Unload()
    {
        if (State is ComponentState.Unloaded or ComponentState.None)
        {
            return true;
        }

        try
        {
            return Plugin.Unload();
        }
        finally
        {
            State = ComponentState.Unloaded;
            ApiRegistry.Unregister(Plugin);
            EventManager.Unregister(Plugin);
            RaiseEvent(PluginEventType.Unloaded);
        }
    }
}