using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlugBotSharp.OneBotProxy;

/// <summary>
/// OneBot 11 的 WebSocket 客户端，由我们主动连到机器人（反向 WS）
/// </summary>
internal sealed class OneBotClient : IDisposable
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement?>> _pending = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// 收到上报事件
    /// </summary>
    public event Action<JsonElement>? EventReceived;

    /// <summary>
    /// 连接状态变化
    /// </summary>
    public event Action<bool>? StateChanged;

    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool Connected => _ws?.State == WebSocketState.Open;

    /// <summary>
    /// 连接
    /// </summary>
    /// <param name="url">ws 地址</param>
    /// <param name="token">访问密钥</param>
    public async Task StartAsync(Uri url, string? token)
    {
        await StopAsync();

        var ws = new ClientWebSocket();

        if (!string.IsNullOrEmpty(token))
        {
            ws.Options.SetRequestHeader("Authorization", $"Bearer {token}");
        }

        _ws = ws;
        _cts = new CancellationTokenSource();

        await ws.ConnectAsync(url, _cts.Token);

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

        foreach (var item in _pending)
        {
            item.Value.TrySetResult(null);
        }

        _pending.Clear();

        StateChanged?.Invoke(false);
    }

    /// <summary>
    /// 调 API，失败或超时返回 null
    /// </summary>
    /// <param name="action">接口名，例如 send_group_msg</param>
    /// <param name="param">参数</param>
    /// <param name="timeoutSeconds">超时时间</param>
    public async Task<JsonElement?> CallAsync(string action, JsonObject? param, int timeoutSeconds = 10)
    {
        var ws = _ws;

        if (ws is null || ws.State != WebSocketState.Open)
        {
            return null;
        }

        var echo = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<JsonElement?>(TaskCreationOptions.RunContinuationsAsynchronously);

        _pending[echo] = tcs;

        try
        {
            var payload = new JsonObject
            {
                ["action"] = action,
                ["params"] = param ?? new JsonObject(),
                ["echo"] = echo
            };

            var bytes = Encoding.UTF8.GetBytes(payload.ToJsonString());

            await _sendLock.WaitAsync();

            try
            {
                await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            finally
            {
                _sendLock.Release();
            }

            var done = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));

            return done == tcs.Task ? await tcs.Task : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            _pending.TryRemove(echo, out _);
        }
    }

    /// <summary>
    /// 接收循环，按 echo 分发 API 响应，其余当事件
    /// </summary>
    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken token)
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

                Handle(text);
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

    /// <summary>
    /// 处理一条报文
    /// </summary>
    private void Handle(string text)
    {
        JsonElement json;

        try
        {
            json = JsonDocument.Parse(text).RootElement.Clone();
        }
        catch
        {
            return;
        }

        if (json.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (json.TryGetProperty("echo", out var echo)
            && echo.ValueKind == JsonValueKind.String
            && _pending.TryGetValue(echo.GetString()!, out var tcs))
        {
            var status = json.TryGetProperty("status", out var st) ? st.GetString() : null;
            var data = json.TryGetProperty("data", out var d) ? d : default;

            tcs.TrySetResult(status is "ok" or "async" ? data : null);

            return;
        }

        if (json.TryGetProperty("post_type", out _))
        {
            EventReceived?.Invoke(json);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _sendLock.Dispose();
    }
}
