using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlugBotSharp.OneBotProxy;

/// <summary>
/// OneBot 11 的正向 HTTP：调机器人的 HTTP API
/// </summary>
internal sealed class OneBotHttpClient : IDisposable
{
    private readonly HttpClient _http = new();

    private string _base = "";
    private string? _token;

    /// <summary>
    /// 设置地址和密钥
    /// </summary>
    /// <param name="baseUrl">API 基址，例如 http://127.0.0.1:5700</param>
    /// <param name="token">访问密钥</param>
    public void Setup(string baseUrl, string? token)
    {
        _base = (baseUrl ?? "").TrimEnd('/');
        _token = token;
    }

    /// <summary>
    /// 调 API，失败或超时返回 null
    /// </summary>
    /// <param name="action">接口名</param>
    /// <param name="param">参数</param>
    /// <param name="timeoutSeconds">超时时间</param>
    public async Task<JsonElement?> CallAsync(string action, JsonObject? param, int timeoutSeconds = 10)
    {
        if (string.IsNullOrEmpty(_base))
        {
            return null;
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_base}/{action}");

            if (!string.IsNullOrEmpty(_token))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            }

            req.Content = new StringContent((param ?? new JsonObject()).ToJsonString(), Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var res = await _http.SendAsync(req, cts.Token);

            var text = await res.Content.ReadAsStringAsync(cts.Token);

            if (!res.IsSuccessStatusCode || string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (!root.TryGetProperty("status", out var status) || status.GetString() is not ("ok" or "async"))
            {
                return null;
            }

            return root.TryGetProperty("data", out var data) ? data.Clone() : default;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _http.Dispose();
    }
}
