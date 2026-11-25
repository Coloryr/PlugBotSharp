using System;
using System.Collections.Generic;
using System.Text;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core.Proxy;

public interface IProxyInstasnce : IBaseComponent
{
    /// <summary>
    /// 开始注册事件
    /// </summary>
    public void RegisterEventHook();
    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="path">实例目录 Instances/{uuid}，配置文件放这里</param>
    /// <param name="uuid">实例UUID</param>
    public void Init(string path, Guid uuid);
    /// <summary>
    /// 状态更新，应该尽快退出
    /// </summary>
    public void Tick();
}