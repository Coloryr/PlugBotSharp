using System;
using System.Collections.Generic;
using System.Text;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core;

public interface IBasePlugin : IComponent
{
    /// <summary>
    /// 注册 / 订阅事件的阶段，过了这个阶段注册器就关闭
    /// </summary>
    /// <param name="register">注册器</param>
    public void RegisterEventHook(IRegister register);
}