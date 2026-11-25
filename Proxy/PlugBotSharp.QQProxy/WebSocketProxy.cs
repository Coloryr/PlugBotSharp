using PlugBotSharp.Core.Messages;
using PlugBotSharp.QQLib;

namespace PlugBotSharp.QQProxy;

/// <summary>
/// 使用 WebSocket 通信的代理，还没实现
/// </summary>
public class WebSocketProxy : IProxy
{
    /// <summary>
    /// 访问凭证
    /// </summary>
    private string? _token;

    /// <inheritdoc/>
    public void SetToken(string? token)
    {
        _token = token;
    }

    /// <inheritdoc/>
    public void Stop()
    {
    }

    /// <inheritdoc/>
    public Task<SendResult> Send(string target, MessageScene scene, BaseMessage message, string? replyId, int sequence)
    {
        return Task.FromResult(new SendResult(null, 0));
    }
}
