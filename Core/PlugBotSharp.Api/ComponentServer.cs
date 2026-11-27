using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace PlugBotSharp.Api;

/// <summary>
/// 组件用的 http / websocket 服务器，由宿主统一创建和关闭
/// </summary>
public sealed class ComponentServer
{
    private readonly ServerOptions _options;

    /// <summary>
    /// 路由配置，拿到 app 之后组件自己 Map
    /// </summary>
    private readonly Action<WebApplication> _configure;

    private WebApplication? _app;

    internal ComponentServer(ServerOptions options, Action<WebApplication> configure)
    {
        _options = options;
        _configure = configure;
    }

    /// <summary>
    /// 访问地址
    /// </summary>
    public string Url => $"{(string.IsNullOrWhiteSpace(_options.Certificate) ? "http" : "https")}://{_options.Host}:{_options.Port}";

    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning => _app is not null;

    /// <summary>
    /// 启动，失败时记日志并返回 false
    /// </summary>
    public bool Start()
    {
        if (_app is not null)
        {
            return true;
        }

        // 证书先按名字找 Certs 目录，找不到再当路径
        var certificate = _options.Certificate;
        var password = _options.CertificatePassword;

        if (!string.IsNullOrWhiteSpace(certificate)
            && !CertStore.TryResolve(certificate, password, out certificate, out password))
        {
            ApiHost.Current?.LogError(null, "找不到证书: " + certificate);

            return false;
        }

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();

        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Parse(_options.Host), _options.Port, options =>
        {
            if (!string.IsNullOrWhiteSpace(certificate))
            {
                options.UseHttps(certificate, password);
            }
        }));

        var app = builder.Build();

        if (_options.WebSocket)
        {
            app.UseWebSockets();
        }

        try
        {
            _configure(app);
            app.StartAsync().GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            ApiHost.Current?.LogError(e, "组件服务器启动失败");

            app.DisposeAsync().AsTask().GetAwaiter().GetResult();

            return false;
        }

        _app = app;

        ApiHost.Current?.LogInfo("组件服务器监听中 " + Url);

        return true;
    }

    /// <summary>
    /// 停止
    /// </summary>
    public void Stop()
    {
        var app = Interlocked.Exchange(ref _app, null);

        if (app is null)
        {
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            app.StopAsync(cts.Token).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            ApiHost.Current?.LogError(e, "组件服务器停止失败");
        }
        finally
        {
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}

/// <summary>
/// 服务器参数
/// </summary>
public sealed record ServerOptions
{
    /// <summary>
    /// 监听地址
    /// </summary>
    public string Host { get; init; } = "127.0.0.1";
    /// <summary>
    /// 监听端口
    /// </summary>
    public ushort Port { get; init; }
    /// <summary>
    /// 证书名（Certs 目录里的）或 pfx 路径，填了就走 https；
    /// 填名字时密码从 Certs.json 或密码文件读，填路径时用 CertificatePassword
    /// </summary>
    public string? Certificate { get; init; }
    /// <summary>
    /// 证书密码，证书填的是名字时以 Certs.json 为准
    /// </summary>
    public string? CertificatePassword { get; init; }
    /// <summary>
    /// 是否启用 WebSocket
    /// </summary>
    public bool WebSocket { get; init; }
}