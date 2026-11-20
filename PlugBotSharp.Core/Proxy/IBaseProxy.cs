using System;
using System.Collections.Generic;
using System.Text;

namespace PlugBotSharp.Core.Proxy;

public interface IBaseProxy
{
    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public bool Init(string path);
    /// <summary>
    /// 加载
    /// </summary>
    /// <returns></returns>
    public bool Load();
    /// <summary>
    /// 卸载
    /// </summary>
    /// <returns></returns>
    public bool Unload();

    /// <summary>
    /// 创建实例
    /// </summary>
    /// <returns></returns>
    public IProxyInstasnce CreateInstance();
    /// <summary>
    /// 配置实例
    /// </summary>
    /// <param name="instasnce"></param>
    public void ConfigInstance(IProxyInstasnce instasnce);

    /// <summary>
    /// 启用通信代理
    /// </summary>
    /// <returns></returns>
    public bool Enable();
    /// <summary>
    /// 禁用通信代理
    /// </summary>
    /// <returns></returns>
    public bool Disable();
}
