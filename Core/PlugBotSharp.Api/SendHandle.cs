using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.Core;

/// <summary>
/// 发送句柄，把消息交给指定实例发出去。
/// 插件拿到的句柄可以发给任意代理，代理 / 实例拿到的句柄不能填自己的代理 id
/// </summary>
public sealed class SendHandle
{
    /// <summary>
    /// 自己的代理 id，插件为 null
    /// </summary>
    private readonly string? _ownProxyId;

    /// <summary>
    /// 自己的实例 uuid，代理组件为 null；发给自己这个实例是回复，是允许的
    /// </summary>
    private readonly Guid? _ownUuid;

    /// <summary>
    /// 来源组件 id，调试面板上显示是谁发的
    /// </summary>
    internal string OwnerId { get; }

    internal SendHandle(string? ownProxyId, Guid? ownUuid = null, string ownerId = "")
    {
        _ownProxyId = ownProxyId;
        _ownUuid = ownUuid;
        OwnerId = ownerId;
    }

    /// <summary>
    /// 发送，目标实例按 context.Uuid 自己过滤
    /// </summary>
    /// <param name="context">消息上下文，Id 是代理组件 id，Uuid 是目标实例</param>
    /// <param name="message">要发的消息</param>
    public Task SendAsync(ProxyMessage context, BaseMessage message)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_ownProxyId is not null
            && string.Equals(context.Id, _ownProxyId, StringComparison.Ordinal)
            && (_ownUuid is null || context.Uuid != _ownUuid))
        {
            throw new InvalidOperationException($"代理 {_ownProxyId} 不能给自己发消息");
        }

        return ApiHost.Host.SendAsync(context, message, OwnerId);
    }
}
