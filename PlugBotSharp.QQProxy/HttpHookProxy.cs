using Microsoft.AspNetCore.Http;

namespace PlugBotSharp.QQProxy;

/// <summary>
/// 把回调注册到宿主 http 服务器的代理
/// </summary>
public class HttpHookProxy : IProxy
{
    /// <summary>
    /// 机器人密钥，用于回调地址验证签名
    /// </summary>
    private readonly string _botSecret;

    /// <summary>
    /// 创建使用宿主 http 服务器的代理
    /// </summary>
    /// <param name="botSecret">机器人密钥</param>
    public HttpHookProxy(string botSecret)
    {
        _botSecret = botSecret ?? string.Empty;
    }

    /// <summary>
    /// 处理平台回调
    /// </summary>
    private Task HandleCallback(HttpContext ctx)
    {
        return CallbackHandler.HandleAsync(ctx, _botSecret);
    }
}
