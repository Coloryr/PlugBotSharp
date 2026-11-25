using System;
using System.Collections.Generic;
using System.Text;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core.Plugin;

public interface IBasePlugin : IComponent
{
    /// <summary>
    /// 开始注册事件
    /// </summary>
    public void RegisterEventHook();
}
