using Microsoft.AspNetCore.Http;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.QQLib;

namespace PlugBotSharp.QQProxy;

/// <summary>
/// 把回调注册到宿主 http 服务器的代理
/// </summary>
public class HttpHookProxy : IProxy
{
    /// <summary>
    /// 所属实例，停止时用来注销接口
    /// </summary>
    private readonly ProxyInstance _instance;

    /// <summary>
    /// 访问凭证
    /// </summary>
    private string? _token;

    /// <summary>
    /// 创建使用宿主 http 服务器的代理
    /// </summary>
    /// <param name="instance">所属实例</param>
    public HttpHookProxy(ProxyInstance instance)
    {
        _instance = instance;

        ComponentApi.RegisterHttpPath(instance, HandleCallback);
    }

    /// <inheritdoc/>
    public void SetToken(string? token)
    {
        _token = token;
    }

    /// <inheritdoc/>
    public void Stop()
    {
        ComponentApi.UnregisterHttpPath(_instance);
    }

    /// <inheritdoc/>
    public Task<SendGroupMessageResObj?> Send(string target, MessageScene scene, BaseMessage message, string? replyId, int sequence)
    {
        return QQApi.Send(_token ?? "", target, scene, message, replyId, sequence);
    }

    /// <summary>
    /// 处理平台回调
    /// </summary>
    private Task HandleCallback(HttpContext ctx)
    {
        return CallbackHandler.HandleAsync(ctx, _instance);
    }
}
