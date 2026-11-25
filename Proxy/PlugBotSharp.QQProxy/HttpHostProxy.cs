using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.QQLib;

namespace PlugBotSharp.QQProxy;

/// <summary>
/// 自己起 http 服务器接收平台回调的代理，服务器由宿主托管
/// </summary>
public class HttpHostProxy : IProxy
{
    /// <summary>
    /// 回调地址路径
    /// </summary>
    private const string CallbackPath = "/";

    /// <summary>
    /// 所属实例，服务器挂在实例上，实例停用会自动关闭
    /// </summary>
    private readonly ProxyInstance _instance;

    /// <summary>
    /// 访问凭证
    /// </summary>
    private string? _token;

    /// <summary>
    /// 宿主托管的服务器
    /// </summary>
    private ComponentServer? _server;

    /// <summary>
    /// 创建使用独立 http 服务器的代理
    /// </summary>
    /// <param name="instance">所属实例</param>
    public HttpHostProxy(ProxyInstance instance)
    {
        _instance = instance;
    }

    /// <inheritdoc/>
    public void SetToken(string? token)
    {
        _token = token;
    }

    /// <inheritdoc/>
    public Task<SendGroupMessageResObj?> Send(string target, MessageScene scene, BaseMessage message, string? replyId, int sequence)
    {
        return QQApi.Send(_token ?? "", target, scene, message, replyId, sequence);
    }

    /// <summary>
    /// 启动 http 服务器
    /// </summary>
    /// <param name="port">监听端口</param>
    /// <param name="certificate">证书名或 pfx 路径，为空走 http</param>
    /// <param name="certificatePassword">证书密码</param>
    public bool Start(ushort port, string? certificate = null, string? certificatePassword = null)
    {
        ProxyInstance.Logger.Info(ProxyLang.GetLang(ProxyLangKey.HttpStart));

        // 平台的回调从公网打进来，不能只听 127.0.0.1
        _server ??= ComponentApi.StartServer(_instance, new ServerOptions
        {
            Host = "0.0.0.0",
            Port = port,
            Certificate = certificate,
            CertificatePassword = certificatePassword
        }, Map);

        if (!_server.Start())
        {
            return false;
        }

        ProxyInstance.Logger.Info(ProxyLang.GetLang(ProxyLangKey.HttpListening, _server.Url, CallbackPath));

        return true;
    }

    /// <summary>
    /// 停止服务器
    /// </summary>
    public void Stop()
    {
        if (_server is null)
        {
            return;
        }

        ProxyInstance.Logger.Info(ProxyLang.GetLang(ProxyLangKey.HttpStop));

        _server.Stop();
    }

    /// <summary>
    /// 注册回调路由
    /// </summary>
    private void Map(WebApplication app)
    {
        app.MapPost(CallbackPath, HandleCallback);
    }

    /// <summary>
    /// 处理平台回调
    /// </summary>
    private Task HandleCallback(HttpContext ctx)
    {
        return CallbackHandler.HandleAsync(ctx, _instance);
    }
}
