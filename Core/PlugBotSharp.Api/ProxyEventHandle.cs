using PlugBotSharp.Api.Messages;

namespace PlugBotSharp.Api;

/// <summary>
/// 代理事件句柄：代理把收到的消息注入进来，宿主自动交给所有订阅消息事件的组件，再把结果收集回来。
/// 只有代理 / 实例能拿到，插件拿不到
/// </summary>
public sealed class ProxyEventHandle
{
    /// <summary>
    /// 创建句柄，一般用 `register.RegisterProxyEvent()` 拿
    /// </summary>
    public ProxyEventHandle()
    {
    }

    /// <summary>
    /// 注入一条消息：所有订阅消息事件的组件都会收到，返回值是它们要回复的消息
    /// </summary>
    /// <param name="message">收到的消息</param>
    public List<BaseMessage> Dispatch(ProxyMessage message)
    {
        return ApiHost.Host.Dispatch(message);
    }
}
