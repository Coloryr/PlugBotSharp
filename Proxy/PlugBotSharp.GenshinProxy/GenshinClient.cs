using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlugBotSharp.GenshinProxy;

/// <summary>
/// 连到 gsuid_core 的 WebSocket 客户端，只往 core 发消息
/// </summary>
internal sealed class GenshinClient : IDisposable
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// 连接状态变化
    /// </summary>
    public event Action<bool>? StateChanged;

    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool Connected => _ws?.State == WebSocketState.Open;

    /// <summary>
    /// 连接，地址是 {Url}/ws/{botId}，有 token 就带上
    /// </summary>
    public async Task StartAsync(Uri url, string botId, string? token)
    {
        await StopAsync();

        var builder = new UriBuilder(url)
        {
            Path = $"/ws/{botId}"
        };

        if (!string.IsNullOrEmpty(token))
        {
            builder.Query = "token=" + Uri.EscapeDataString(token);
        }

        var ws = new ClientWebSocket();

        _ws = ws;
        _cts = new CancellationTokenSource();

        await ws.ConnectAsync(builder.Uri, _cts.Token);

        StateChanged?.Invoke(true);
        _ = Task.Run(() => ReceiveLoopAsync(ws, _cts.Token));
    }

    /// <summary>
    /// 断开
    /// </summary>
    public async Task StopAsync()
    {
        var cts = Interlocked.Exchange(ref _cts, null);
        var ws = Interlocked.Exchange(ref _ws, null);

        cts?.Cancel();
        cts?.Dispose();

        if (ws is not null)
        {
            try
            {
                if (ws.State == WebSocketState.Open)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", timeout.Token);
                }
            }
            catch
            {
                // 断开失败不影响后续
            }

            ws.Dispose();
        }

        StateChanged?.Invoke(false);
    }

    /// <summary>
    /// 把平台消息发给 core
    /// </summary>
    public async Task SendAsync(GsMessageReceive message)
    {
        var ws = _ws;

        if (ws is null || ws.State != WebSocketState.Open)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, GenshinJsonContext.Default.GsMessageReceive));

        await _sendLock.WaitAsync();

        try
        {
            await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// 接收循环，只把数据读掉，避免缓冲区堆积和错过关闭帧
    /// </summary>
    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken token)
    {
        var buffer = new byte[8192];

        try
        {
            while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(buffer, token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch
        {
            // 连接断开，交给上层重连
        }
        finally
        {
            StateChanged?.Invoke(false);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _sendLock.Dispose();
    }
}
