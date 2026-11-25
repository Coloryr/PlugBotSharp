using System;
using System.Collections.Generic;
using System.Text;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.Core.Proxy;

public interface IProxyInstasnce : IBaseComponent
{
    /// <summary>
    /// 注册 / 订阅事件的阶段，过了这个阶段注册器就关闭
    /// </summary>
    /// <param name="register">注册器</param>
    public void RegisterEventHook(IRegister register);
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
    /// <summary>
    /// 发一条消息到平台：宿主把别的组件的发送请求转过来，拦截器已经放行
    /// </summary>
    /// <param name="message">消息上下文，取回复目标和被动回复 id</param>
    /// <param name="reply">要发的消息</param>
    public Task SendAsync(ProxyMessage message, BaseMessage reply);
}