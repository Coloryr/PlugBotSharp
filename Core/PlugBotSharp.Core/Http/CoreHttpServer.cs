using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;

namespace PlugBotSharp.Core.Http;

/// <summary>
/// 管理面板 Http 服务器
/// </summary>
internal sealed class CoreHttpServer
{
    /// <summary>
    /// 上传安装包的大小上限，256 MB
    /// </summary>
    private const long MaxUploadSize = 256L * 1024 * 1024;

    private readonly Logger _logger = LogManager.GetLogger(nameof(CoreHttpServer));
    private readonly DateTime _startTime = DateTime.Now;

    private WebApplication? _app;

    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning => _app is not null;

    /// <summary>
    /// 是否用的是 https
    /// </summary>
    public bool IsHttps { get; private set; }

    /// <summary>
    /// 启动服务器
    /// </summary>
    /// <param name="port">监听端口</param>
    /// <param name="certificate">pfx 证书路径，为空走 http</param>
    /// <param name="certificatePassword">证书密码</param>
    public async Task<bool> Start(ushort port, string? certificate = null, string? certificatePassword = null)
    {
        if (_app is not null)
        {
            return true;
        }

        _logger.Info(BotLang.GetLang(BotLangKey.HttpStart));

        IsHttps = !string.IsNullOrWhiteSpace(certificate);

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.Listen(IPAddress.Loopback, port, options =>
            {
                if (!string.IsNullOrWhiteSpace(certificate))
                {
                    options.UseHttps(certificate, certificatePassword);
                }
            });

            // 组件安装包可能带上 native 库，放宽请求体大小
            k.Limits.MaxRequestBodySize = MaxUploadSize;
        });

        builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = MaxUploadSize);

        // Ctrl+C 时走正常关闭流程，让 WaitForShutdown 能返回
        builder.Host.UseConsoleLifetime();

        var app = builder.Build();

        // /api/admin 需要登录，组件接口和静态资源不拦
        app.Use(AuthGuard);

        // 面板接口先注册，具体的路由比 /api/{**path} 优先
        AuthApi.Map(app);
        AdminApi.Map(app);

        app.MapGet("/api/status", Status);
        app.Map("/api/{**path}", Api);
        app.MapGet("/{**path}", Serve);

        try
        {
            await app.StartAsync();
        }
        catch (Exception e)
        {
            _logger.Error(e, BotLang.GetLang(BotLangKey.HttpStartError));
            await app.DisposeAsync();
            return false;
        }

        _app = app;
        _logger.Info(BotLang.GetLang(BotLangKey.HttpListening, IsHttps ? "https" : "http", port));
        return true;
    }

    /// <summary>
    /// 阻塞到服务器停止，Ctrl+C 或调用 Stop 后返回
    /// </summary>
    public void WaitForShutdown()
    {
        var app = _app;

        if (app is null)
        {
            return;
        }

        // StartAsync 只等到开始监听，这里才是真正让进程停住的地方
        app.WaitForShutdownAsync().GetAwaiter().GetResult();
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

        _logger.Info(BotLang.GetLang(BotLangKey.HttpStop));

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await app.StopAsync(cts.Token);
        }
        catch (Exception e)
        {
            _logger.Error(e, BotLang.GetLang(BotLangKey.HttpStopError));
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    /// <summary>
    /// 管理接口的登录校验
    /// </summary>
    private static async Task AuthGuard(HttpContext ctx, RequestDelegate next)
    {
        var path = ctx.Request.Path.Value ?? "";

        if (path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase) && AuthApi.Authenticate(ctx) is null)
        {
            await ApiUtil.Fail(BotLang.GetLang(BotLangKey.AuthRequired), StatusCodes.Status401Unauthorized).ExecuteAsync(ctx);
            return;
        }

        await next(ctx);
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
            plugins = ComponentManager.Plugins.Count,
            proxies = ComponentManager.Proxys.Count,
        });
    }

    /// <summary>
    /// 组件/实例注册的接口，路径 /api/{**path}
    /// </summary>
    private static async Task Api(HttpContext ctx)
    {
        var path = (ctx.Request.Path.Value ?? "").TrimStart('/');

        if (path.Length == 0 || !ApiRegistry.TryGet(path, out var call))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        try
        {
            await call!(ctx);
        }
        catch (Exception e)
        {
            BotManager.Logger.Error(e, BotLang.GetLang(BotLangKey.HttpApiError, "/" + path));

            if (!ctx.Response.HasStarted)
            {
                ctx.Response.Clear();
                ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }
        }
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

        // 未注册的 /api 接口不落到前端页面
        if (path.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
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