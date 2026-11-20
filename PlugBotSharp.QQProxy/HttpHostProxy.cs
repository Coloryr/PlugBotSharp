using System;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

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
    /// 机器人密钥，用于回调地址验证签名
    /// </summary>
    private readonly string _botSecret;

    /// <summary>
    /// 创建使用独立 http 服务器的代理
    /// </summary>
    /// <param name="botSecret">机器人密钥</param>
    public HttpHostProxy(string botSecret)
    {
        _botSecret = botSecret ?? string.Empty;
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

    /// <summary>
    /// 停止 http 服务器
    /// </summary>
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
        return CallbackHandler.HandleAsync(ctx, _botSecret);
    }
}