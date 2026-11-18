using NLog;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Proxy;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PlugBotSharp.QQProxy;

public class ProxyInstance : IProxyInstasnce
{
    private Logger _logger;

    public const string Url = "https://api.bot.qq.com/";

    /// <summary>
    /// 回调地址路径
    /// </summary>
    private const string CallbackPath = "/";

    private string _configFile;
    private InstanceDataObj _data;
    private WebApplication? _app;
    private readonly HttpClient _httpClient = new();

    private string _token;
    private long _expires;
    private DateTime _lastDateTime;

    public void Init(string path, Guid uuid)
    {
        _configFile = Path.Combine(path, "config.json");
        _logger = LogManager.GetLogger($"QQBot[{uuid.ToString()[0..8]}]");
        _logger.Info(ProxyLang.GetLang(ProxyLangKey.InstanceInit));
    }

    public async Task<bool> Load()
    {
        _logger.Info("Start load");

        try
        {
            if (File.Exists(_configFile))
            {
                var obj = JsonUtils.ToObj(File.ReadAllText(_configFile), JsonContext.Default.InstanceDataObj);
                if (obj != null)
                {
                    _logger.Info("Config read");
                    _data = obj;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Config read error", ex);
        }

        _data = new InstanceDataObj();

        return false;
    }

    public async Task<bool> Enable()
    {
        _logger.Info("Enable");

        if (string.IsNullOrWhiteSpace(_data.AppId)
        || string.IsNullOrWhiteSpace(_data.AppSecret))
        {
            _logger.Warn("bot info is empty");

            return false;
        }

        return await GetToken();
    }

    public async Task<bool> Disable()
    {
        _logger.Info("Disable");

        return true;
    }

    public async Task<bool> Run()
    {
        _logger.Info("Start http server");

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, _data.Port));

        var app = builder.Build();
        app.MapPost(CallbackPath, HandleCallback);

        try
        {
            await app.StartAsync();
        }
        catch (Exception e)
        {
            _logger.Error("Start http server error", e);
            await app.DisposeAsync();
            return false;
        }

        _app = app;
        _logger.Info($"Listening on http://127.0.0.1:{_data.Port}{CallbackPath}");
        return true;
    }

    private async Task HandleCallback(HttpContext ctx)
    {
        // 回调签名基于原始请求体，Body 只能读一次
        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms, ctx.RequestAborted);
        var body = Encoding.UTF8.GetString(ms.ToArray());

        _logger.Debug($"Callback {body}");

        // TODO: 校验 X-Signature-Ed25519 / X-Signature-Timestamp
        // TODO: op == 13 时返回 plain_token + signature 完成回调地址验证

        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync("""{"op":12}""", ctx.RequestAborted);
    }

    public void Tick()
    {

    }

    public void Stop()
    {
        var app = Interlocked.Exchange(ref _app, null);
        if (app is null)
        {
            return;
        }

        _logger.Info("Stop http server");

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            app.StopAsync(cts.Token).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            _logger.Error("Stop http server error", e);
        }
        finally
        {
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private async Task<bool> GetToken()
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync($"{Url}app/getAppAccessToken", new GetAppAccessTokenObj()
            {
                AppId = _data.AppId,
                Clientecret = _data.AppSecret
            }, JsonContext.Default.GetAppAccessTokenObj);

            var data = await res.Content.ReadFromJsonAsync(JsonContext.Default.GetAppAccessTokenResObj);
            if (data == null || string.IsNullOrWhiteSpace(data.AccessToken) || data.Code != 0)
            {
                _logger.Error($"Get token fail {data?.Code} {data?.Message}");
                return false;
            }

            _token = data.AccessToken;
            _expires = long.Parse(data.ExpiresIn ?? "0");
            _lastDateTime = DateTime.Now;

            return true;
        }
        catch (Exception e)
        {
            _logger.Error("Get token error", e);
        }

        return false;
    }
}
