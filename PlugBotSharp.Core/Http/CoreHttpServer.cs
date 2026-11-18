using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NLog;

namespace PlugBotSharp.Core.Http;

/// <summary>
/// 管理面板 Http 服务器
/// </summary>
public sealed class CoreHttpServer
{
    private readonly Logger _logger = LogManager.GetLogger(nameof(CoreHttpServer));
    private readonly DateTime _startTime = DateTime.Now;

    private WebApplication? _app;

    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning => _app is not null;

    /// <summary>
    /// 启动服务器
    /// </summary>
    /// <param name="port">监听端口</param>
    public async Task<bool> Start(ushort port)
    {
        if (_app is not null)
        {
            return true;
        }

        _logger.Info("Start admin panel");

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, port));

        var app = builder.Build();

        app.MapGet("/api/status", Status);
        app.MapGet("/{**path}", Serve);

        try
        {
            await app.StartAsync();
        }
        catch (Exception e)
        {
            _logger.Error("Start admin panel error", e);
            await app.DisposeAsync();
            return false;
        }

        _app = app;
        _logger.Info($"Admin panel listening on http://127.0.0.1:{port}");
        return true;
    }

    /// <summary>
    /// 停止服务器
    /// </summary>
    public async Task Stop()
    {
        var app = Interlocked.Exchange(ref _app, null);
        if (app is null)
        {
            return;
        }

        _logger.Info("Stop admin panel");

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await app.StopAsync(cts.Token);
        }
        catch (Exception e)
        {
            _logger.Error("Stop admin panel error", e);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    /// <summary>
    /// 状态接口
    /// </summary>
    private IResult Status()
    {
        return Results.Json(new
        {
            version = typeof(CoreHttpServer).Assembly.GetName().Version?.ToString(),
            uptime = (DateTime.Now - _startTime).ToString(@"d\.hh\:mm\:ss"),
            plugins = BotManager.Plugins.Count,
            proxies = BotManager.Proxys.Count,
        });
    }

    /// <summary>
    /// 前端资源，找不到时回退到 index.html
    /// </summary>
    private static async Task Serve(HttpContext ctx)
    {
        var path = (ctx.Request.Path.Value ?? "/").TrimStart('/');

        if (path.Length == 0)
        {
            path = "index.html";
        }

        var stream = EmbeddedAssets.Open(path);

        if (stream is null)
        {
            // 前端路由（非静态文件）回退到 index.html
            if (path.Contains('.'))
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            path = "index.html";
            stream = EmbeddedAssets.Open(path);
        }

        if (stream is null)
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            await ctx.Response.WriteAsync("Admin panel not found", ctx.RequestAborted);
            return;
        }

        await using (stream)
        {
            ctx.Response.ContentType = EmbeddedAssets.GetContentType(path);
            ctx.Response.Headers.CacheControl = path.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)
                ? "public,max-age=31536000,immutable"
                : "no-store";

            await stream.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
        }
    }
}
