using PlugBotSharp.Api;
using PlugBotSharp.Core.Http;

namespace PlugBotSharp.Core;

/// <summary>
/// 管理面板 http 服务器
/// </summary>
internal static class HttpManager
{
    /// <summary>
    /// 当前服务器
    /// </summary>
    internal static CoreHttpServer? Server { get; private set; }

    /// <summary>
    /// 当前监听端口，没启动时为 0
    /// </summary>
    internal static ushort Port { get; private set; }

    /// <summary>
    /// 访问协议，配了证书是 https
    /// </summary>
    internal static string Scheme { get; private set; } = "http";

    /// <summary>
    /// 面板访问地址
    /// </summary>
    internal static string Address => $"{Scheme}://127.0.0.1:{Port}";

    /// <summary>
    /// 启动服务器，失败直接退出程序
    /// </summary>
    /// <param name="port">监听端口</param>
    /// <param name="certificate">pfx 证书路径</param>
    /// <param name="certificatePassword">证书密码</param>
    public static void Start(ushort port, string? certificate = null, string? certificatePassword = null)
    {
        if (Server is not null)
        {
            return;
        }

        var server = new CoreHttpServer();

        // 证书先按名字找 Certs 目录，找不到再当路径用
        var file = certificate;
        var password = certificatePassword;

        if (!string.IsNullOrWhiteSpace(certificate))
        {
            if (!CertStore.TryResolve(certificate, certificatePassword, out file, out password))
            {
                BotManager.Logger.Error(BotLang.GetLang(BotLangKey.CertNotFound, certificate));
                Environment.Exit(1);
            }
        }

        if (!server.Start(port, file, password).GetAwaiter().GetResult())
        {
            BotManager.Logger.Error(BotLang.GetLang(BotLangKey.PanelStartFail));
            Environment.Exit(1);
        }

        Server = server;
        Port = port;
        Scheme = server.IsHttps ? "https" : "http";
    }

    /// <summary>
    /// 阻塞到服务器停止，进程保持运行
    /// </summary>
    public static void WaitForShutdown()
    {
        Server?.WaitForShutdown();
    }

    /// <summary>
    /// 停止服务器
    /// </summary>
    public static void Stop()
    {
        if (Server is { } server)
        {
            Server = null;
            Port = 0;
            server.Stop().GetAwaiter().GetResult();
        }
    }
}