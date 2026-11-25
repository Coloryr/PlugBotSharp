using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;

namespace PlugBotSharp.OneBotProxy;

/// <summary>
/// OneBot 的反向 WS / 反向 HTTP 服务端，服务器由宿主托管
/// </summary>
internal sealed class OneBotServer
{
    /// <summary>
    /// WebSocket 上报路径
    /// </summary>
    public const string WebSocketPath = "/ws";

    private ComponentServer? _server;
    private string? _token;

    /// <summary>
    /// 收到上报事件
    /// </summary>
    public event Action<JsonElement>? EventReceived;

    /// <summary>
    /// 启动监听，服务器挂在实例上，实例停用会自动关闭
    /// </summary>
    /// <param name="owner">所属实例</param>
    /// <param name="host">监听地址</param>
    /// <param name="port">监听端口</param>
    /// <param name="token">访问密钥</param>
    /// <param name="webSocket">是否接受 WebSocket 连接</param>
    /// <param name="http">是否接受 HTTP 上报</param>
    /// <param name="certificate">证书名或 pfx 路径，为空走 http</param>
    /// <param name="certificatePassword">证书密码</param>
    public bool Start(object owner, string host, int port, string? token, bool webSocket, bool http,
        string? certificate = null, string? certificatePassword = null)
    {
        _token = token;
        _server?.Stop();

        _server = ComponentApi.StartServer(owner, new ServerOptions
        {
            Host = host,
            Port = (ushort)port,
            Certificate = certificate,
            CertificatePassword = certificatePassword,
            WebSocket = webSocket
        }, app =>
        {
            if (http)
            {
                app.MapPost("/", HandleHttpAsync);
            }

            if (webSocket)
            {
                app.Map(WebSocketPath, HandleWebSocketAsync);
            }
        });

        return _server.Start();
    }

    /// <summary>
    /// 停止监听
    /// </summary>
    public void Stop()
    {
        _server?.Stop();
    }

    /// <summary>
    /// 反向 HTTP：机器人把事件 POST 过来
    /// </summary>
    private async Task HandleHttpAsync(HttpContext ctx)
    {
        if (!CheckToken(ctx.Request))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var reader = new StreamReader(ctx.Request.Body);
        var text = await reader.ReadToEndAsync(ctx.RequestAborted);

        Raise(text);

        ctx.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    /// <summary>
    /// 反向 WS：机器人连过来推事件
    /// </summary>
    private async Task HandleWebSocketAsync(HttpContext ctx)
    {
        if (!ctx.WebSockets.IsWebSocketRequest || !CheckToken(ctx.Request))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();

        await ReceiveAsync(ws, ctx.RequestAborted);
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

            if (json.ValueKind == JsonValueKind.Object && IsEvent(json))
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
    /// v11 用 post_type 标记事件，v12 用 type
    /// </summary>
    private static bool IsEvent(JsonElement json)
    {
        return json.TryGetProperty("post_type", out _)
            || (json.TryGetProperty("type", out _) && json.TryGetProperty("self", out _));
    }

    /// <summary>
    /// 校验 Bearer 密钥
    /// </summary>
    private bool CheckToken(HttpRequest request)
    {
        if (string.IsNullOrEmpty(_token))
        {
            return true;
        }

        return request.Headers.Authorization.ToString() == $"Bearer {_token}";
    }
}
