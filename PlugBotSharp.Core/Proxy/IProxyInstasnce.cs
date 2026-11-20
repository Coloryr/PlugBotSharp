using System;
using System.Collections.Generic;
using System.Text;

namespace PlugBotSharp.Core.Proxy;

public interface IProxyInstasnce
{
    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="path">实例目录 Instances/{uuid}，配置文件放这里</param>
    /// <param name="uuid">实例UUID</param>
    public void Init(string path, Guid uuid);

    /// <summary>
    /// 开始加载，这里做配置文件读取
    /// </summary>
    /// <returns></returns>
    public Task<bool> Load();

    /// <summary>
    /// 实例启用
    /// </summary>
    /// <returns></returns>
    public Task<bool> Enable();
    /// <summary>
    /// 实例禁用
    /// </summary>
    /// <returns></returns>
    public Task<bool> Disable();

    /// <summary>
    /// 状态更新，应该尽快退出
    /// </summary>
    public void Tick();
}
