using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PlugBotSharp.OneBotProxy;

/// <summary>
/// OneBot 11 的反向 WS / 反向 HTTP 服务端，由机器人连过来或者把事件 POST 过来
/// </summary>
internal sealed class OneBotServer : IAsyncDisposable
{
    /// <summary>
    /// WebSocket 上报路径
    /// </summary>
    public const string WebSocketPath = "/ws";

    private WebApplication? _app;

    /// <summary>
    /// 收到上报事件
    /// </summary>
    public event Action<JsonElement>? EventReceived;

    /// <summary>
    /// 启动监听
    /// </summary>
    /// <param name="host">监听地址</param>
    /// <param name="port">监听端口</param>
    /// <param name="token">访问密钥</param>
    public async Task StartAsync(string host, int port, string? token)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Parse(host), port));

        var app = builder.Build();

        app.UseWebSockets();

        // 反向 HTTP：机器人把事件 POST 过来
        app.MapPost("/", async ctx =>
        {
            if (!CheckToken(ctx.Request, token))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            using var reader = new StreamReader(ctx.Request.Body);
            var text = await reader.ReadToEndAsync(ctx.RequestAborted);
            Raise(text);

            ctx.Response.StatusCode = StatusCodes.Status204NoContent;
        });

        // 反向 WS：机器人连过来推事件
        app.Map(WebSocketPath, async ctx =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest || !CheckToken(ctx.Request, token))
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            await ReceiveAsync(ws, ctx.RequestAborted);
        });

        await app.StartAsync();

        _app = app;
    }

    /// <summary>
    /// 停止监听
    /// </summary>
    public async Task StopAsync()
    {
        var app = Interlocked.Exchange(ref _app, null);

        if (app is null)
        {
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await app.StopAsync(cts.Token);
        }
        catch
        {
            // 停止失败不影响调用方
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    /// <summary>
    /// 单个 websocket 连接的接收循环
    /// </summary>
    private async Task ReceiveAsync(WebSocket ws, CancellationToken token)
    {
        var buffer = new byte[8192];
        using var builder = new MemoryStream();

        try
        {
            while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(buffer, token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                builder.Write(buffer, 0, result.Count);

                if (!result.EndOfMessage)
                {
                    continue;
                }

                var text = Encoding.UTF8.GetString(builder.ToArray());
                builder.SetLength(0);

                Raise(text);
            }
        }
        catch
        {
            // 连接断开
        }
    }

    /// <summary>
    /// 把上报内容交给上层
    /// </summary>
    private void Raise(string text)
    {
        try
        {
            var json = JsonDocument.Parse(text).RootElement.Clone();

            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("post_type", out _))
            {
                EventReceived?.Invoke(json);
            }
        }
        catch
        {
            // 不是合法 json，丢掉
        }
    }

    /// <summary>
    /// 校验 Bearer 密钥
    /// </summary>
    private static bool CheckToken(HttpRequest request, string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return true;
        }

        var header = request.Headers.Authorization.ToString();

        return header == $"Bearer {token}";
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
