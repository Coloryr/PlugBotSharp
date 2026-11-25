using System;
using System.Collections.Generic;
using System.Text;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core.Proxy;

public interface IBaseProxy : IComponent
{

    /// <summary>
    /// 创建实例
    /// </summary>
    /// <returns></returns>
    public IProxyInstasnce CreateInstance();
    /// <summary>
    /// 实例重载配置
    /// </summary>
    /// <param name="instasnce"></param>
    public void Reload(IProxyInstasnce instasnce);
}
