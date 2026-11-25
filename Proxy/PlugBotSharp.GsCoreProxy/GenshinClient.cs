using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlugBotSharp.GsCoreProxy;

/// <summary>
/// 连到 gsuid_core 的 WebSocket 客户端，收发都走这里
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
    /// core 下发的消息
    /// </summary>
    public event Action<GsMessageReceive>? MessageReceived;

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
            // gsucore 用 receive_bytes() 读，必须发二进制帧，发 text 会把它整条连接打崩
            await ws.SendAsync(bytes, WebSocketMessageType.Binary, true, CancellationToken.None);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// 接收循环，把整帧拼起来再解析，core 下发的消息交给上层
    /// </summary>
    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken token)
    {
        var buffer = new byte[8192];
        using var frame = new MemoryStream();

        try
        {
            while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                frame.SetLength(0);

                WebSocketReceiveResult result;

                do
                {
                    result = await ws.ReceiveAsync(buffer, token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        try
                        {
                            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", token);
                        }
                        catch
                        {
                            // 回关闭帧失败不影响退出
                        }

                        return;
                    }

                    frame.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                HandleFrame(frame.ToArray());
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
    /// 解析一帧，认不出来只记日志
    /// </summary>
    /// <param name="payload">帧内容</param>
    internal void HandleFrame(byte[] payload)
    {
        try
        {
            var text = Encoding.UTF8.GetString(payload);
            var message = JsonSerializer.Deserialize(text, GenshinJsonContext.Default.GsMessageReceive);

            if (message is null)
            {
                return;
            }

            MessageReceived?.Invoke(message);
        }
        catch (Exception e)
        {
            GenshinLang.Log.Warn(e, GenshinLang.Get(GenshinLangKey.CoreSendFail));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _sendLock.Dispose();
    }
}
