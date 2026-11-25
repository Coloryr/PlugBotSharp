using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.Core;

/// <summary>
/// 消息事件（长任务版）：插件收到消息后可以慢慢处理，处理完用 Reply 把消息发回发起这次分发的代理实例
/// </summary>
public sealed class MessageEvent
{
    internal MessageEvent(ProxyMessage message, Func<BaseMessage, Task> reply)
    {
        Message = message;
        Reply = reply;
    }

    /// <summary>
    /// 收到的消息
    /// </summary>
    public ProxyMessage Message { get; }

    /// <summary>
    /// 回复：消息会发回发起这次分发的代理实例，长任务结束后也能调，可以调多次
    /// </summary>
    public Func<BaseMessage, Task> Reply { get; }
}
