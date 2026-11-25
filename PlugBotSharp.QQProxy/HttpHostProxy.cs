using System;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.QQLib;

namespace PlugBotSharp.QQProxy;

public class HttpHostProxy : IProxy
{
    /// <summary>
    /// Http服务器
    /// </summary>
    private WebApplication? _app;

    /// <summary>
    /// 回调地址路径
    /// </summary>
    private const string CallbackPath = "/";

    /// <summary>
    /// 所属实例
    /// </summary>
    private readonly ProxyInstance _instance;

    /// <summary>
    /// 访问凭证
    /// </summary>
    private string? _token;

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
    public async Task Start(ushort port)
    {
        ProxyInstance.Logger.Info(ProxyLang.GetLang(ProxyLangKey.HttpStart));

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, port));

        var app = builder.Build();
        app.MapPost(CallbackPath, HandleCallback);

        try
        {
            await app.StartAsync();
        }
        catch (Exception e)
        {
            ProxyInstance.Logger.Error(e, ProxyLang.GetLang(ProxyLangKey.HttpStartError));
            await app.DisposeAsync();
            return;
        }

        _app = app;
        ProxyInstance.Logger.Info(ProxyLang.GetLang(ProxyLangKey.HttpListening, port, CallbackPath));
    }

    /// <inheritdoc/>
    public void Stop()
    {
        var app = Interlocked.Exchange(ref _app, null);
        if (app is null)
        {
            return;
        }

        ProxyInstance.Logger.Info(ProxyLang.GetLang(ProxyLangKey.HttpStop));

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            app.StopAsync(cts.Token).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            ProxyInstance.Logger.Error(e, ProxyLang.GetLang(ProxyLangKey.HttpStopError));
        }
        finally
        {
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// 处理平台回调
    /// </summary>
    private Task HandleCallback(HttpContext ctx)
    {
        return CallbackHandler.HandleAsync(ctx, _instance);
    }
}
