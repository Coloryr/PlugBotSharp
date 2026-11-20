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
    /// 启动服务器，失败直接退出程序
    /// </summary>
    /// <param name="port">监听端口</param>
    public static void Start(ushort port)
    {
        if (Server is not null)
        {
            return;
        }

        var server = new CoreHttpServer();

        if (!server.Start(port).GetAwaiter().GetResult())
        {
            BotManager.Logger.Error(BotLang.GetLang(BotLangKey.PanelStartFail));
            Environment.Exit(1);
        }

        Server = server;
        Port = port;
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
