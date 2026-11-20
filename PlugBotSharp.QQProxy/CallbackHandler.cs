using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Crypto;

namespace PlugBotSharp.QQProxy;

/// <summary>
/// 平台回调处理，op 13 是回调地址验证，op 0 是事件推送
/// </summary>
internal static class CallbackHandler
{
    /// <summary>
    /// 收到回调后的应答
    /// </summary>
    private const string Ack = """{"op":12}""";

    /// <summary>
    /// 回调地址验证
    /// </summary>
    private const int OpValidation = 13;

    /// <summary>
    /// 处理平台回调
    /// </summary>
    /// <param name="ctx">http 上下文</param>
    /// <param name="botSecret">机器人密钥</param>
    public static async Task HandleAsync(HttpContext ctx, string botSecret)
    {
        // 回调签名基于原始请求体，Body 只能读一次
        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms, ctx.RequestAborted);
        var body = Encoding.UTF8.GetString(ms.ToArray());

        ProxyInstance.Logger.Debug(ProxyLang.GetLang(ProxyLangKey.Callback, body));

        BaseMessageObj? obj;
        try
        {
            obj = JsonUtils.ToObj(body, JsonContext.Default.BaseMessageObj);
        }
        catch (JsonException e)
        {
            ProxyInstance.Logger.Error(e, ProxyLang.GetLang(ProxyLangKey.CallbackParseError));
            await WriteAsync(ctx, Ack);
            return;
        }

        if (obj == null)
        {
            await WriteAsync(ctx, Ack);
            return;
        }

        if (obj.OpCode == OpValidation)
        {
            await HandleValidationAsync(ctx, botSecret, obj);
            return;
        }

        // 事件推送，返回 op 12 告诉平台已经收到
        ProxyInstance.Logger.Debug(ProxyLang.GetLang(ProxyLangKey.CallbackEvent, obj.Type));
        await WriteAsync(ctx, Ack);
    }

    /// <summary>
    /// 处理回调地址验证，把 event_ts + plain_token 用机器人密钥签名后返回
    /// </summary>
    private static async Task HandleValidationAsync(HttpContext ctx, string botSecret, BaseMessageObj obj)
    {
        ProxyInstance.Logger.Info(ProxyLang.GetLang(ProxyLangKey.CallbackValidation));

        if (string.IsNullOrWhiteSpace(botSecret))
        {
            ProxyInstance.Logger.Error(ProxyLang.GetLang(ProxyLangKey.BotInfoEmpty));
            await WriteAsync(ctx, Ack);
            return;
        }

        var request = obj.Data == null
            ? null
            : JsonUtils.ToObj(obj.Data.ToJsonString(), JsonContext.Default.ValidationRequestObj);

        if (request == null
        || string.IsNullOrEmpty(request.PlainToken)
        || string.IsNullOrEmpty(request.EventTs))
        {
            ProxyInstance.Logger.Error(ProxyLang.GetLang(ProxyLangKey.CallbackValidationFail));
            await WriteAsync(ctx, Ack);
            return;
        }

        var seed = DeriveSeed(botSecret);
        var message = Encoding.UTF8.GetBytes(request.EventTs + request.PlainToken);
        var signature = Convert.ToHexStringLower(Ed25519.Sign(seed, message));

        var response = new ValidationResponseObj
        {
            PlainToken = request.PlainToken,
            Signature = signature
        };

        await WriteAsync(ctx, JsonUtils.ToString(response, JsonContext.Default.ValidationResponseObj));
    }

    /// <summary>
    /// 由机器人密钥派生出 32 字节种子，密钥不足 32 字节时循环拼接
    /// </summary>
    /// <param name="botSecret">机器人密钥</param>
    /// <returns>私钥种子</returns>
    public static byte[] DeriveSeed(string botSecret)
    {
        var seed = new byte[Ed25519.SeedSize];
        var source = Encoding.UTF8.GetBytes(botSecret ?? string.Empty);

        if (source.Length == 0)
        {
            return seed;
        }

        for (var i = 0; i < seed.Length; i++)
        {
            seed[i] = source[i % source.Length];
        }

        return seed;
    }

    /// <summary>
    /// 写 json 响应
    /// </summary>
    private static async Task WriteAsync(HttpContext ctx, string json)
    {
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(json, ctx.RequestAborted);
    }
}
