using System;
using System.Collections.Generic;
using System.Runtime.Loader;
using System.Text;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core;

/// <summary>
/// 组件生命周期状态
/// </summary>
internal enum ComponentState
{
    None,
    Inited,
    Loaded,
    Enabled,
    Disabled,
    Unloaded
}

internal abstract class BaseAssembly
{
    /// <summary>
    /// 组件元数据
    /// </summary>
    public required MetaObj Meta { get; init; }

    /// <summary>
    /// 组件所在目录
    /// </summary>
    public required string Folder { get; init; }

    /// <summary>
    /// 组件加载上下文，卸载后置空
    /// </summary>
    public AssemblyLoadContext? Context { get; internal set; }

    /// <summary>
    /// 生命周期状态
    /// </summary>
    public ComponentState State { get; protected set; } = ComponentState.None;

    /// <summary>
    /// 配置目录
    /// </summary>
    protected string ConfigPath => ComponentManager.GetConfigPath(Meta.Id);

    /// <summary>
    /// 断开对加载上下文和实例的引用，方便 AssemblyLoadContext 被回收
    /// </summary>
    internal virtual void Detach()
    {
        Context = null;
    }

    /// <summary>
    /// 记录生命周期日志
    /// </summary>
    protected void Trace(BotLangKey key)
    {
        BotManager.Logger?.Info(BotLang.GetLang(key, Meta.Id));
    }
}
