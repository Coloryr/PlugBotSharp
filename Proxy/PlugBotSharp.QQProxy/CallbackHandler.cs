using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Crypto;
using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.QQProxy;

/// <summary>
/// 平台回调处理，op 13 是回调地址验证，op 0 是事件推送
/// </summary>
internal static class CallbackHandler
{
    /// <summary>
    /// 回调地址验证
    /// </summary>
    private const int OpValidation = 13;

    /// <summary>
    /// 事件推送
    /// </summary>
    private const int OpDispatch = 0;

    /// <summary>
    /// 平台签名头
    /// </summary>
    private const string SignatureHeader = "X-Signature-Ed25519";

    /// <summary>
    /// 平台签名时间戳头
    /// </summary>
    private const string TimestampHeader = "X-Signature-Timestamp";

    /// <summary>
    /// 处理平台回调
    /// </summary>
    /// <param name="ctx">http 上下文</param>
    /// <param name="instance">所属实例</param>
    public static async Task HandleAsync(HttpContext ctx, ProxyInstance instance)
    {
        // 回调签名基于原始请求体，Body 只能读一次
        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms, ctx.RequestAborted);
        var body = Encoding.UTF8.GetString(ms.ToArray());

        // 调试开关：打开组件配置 LogPayload 才把收到的数据原样记下来
        if (ProxyMain.LogPayload)
        {
            ProxyInstance.Logger.Debug(ProxyLang.GetLang(ProxyLangKey.Callback,
                $"{ctx.Request.Method} {ctx.Request.Path} from {ctx.Connection.RemoteIpAddress} {body}"));

            foreach (var header in ctx.Request.Headers)
            {
                ProxyInstance.Logger.Debug($"{header.Key}: {header.Value}");
            }
        }

        BaseMessageObj? obj;
        try
        {
            obj = JsonUtils.ToObj(body, JsonContext.Default.BaseMessageObj);
        }
        catch (JsonException e)
        {
            ProxyInstance.Logger.Error(e, ProxyLang.GetLang(ProxyLangKey.CallbackParseError));
            await WriteOkAsync(ctx);
            return;
        }

        if (obj == null)
        {
            await WriteOkAsync(ctx);
            return;
        }

        if (obj.OpCode == OpValidation)
        {
            await HandleValidationAsync(ctx, instance.BotSecret, obj);
            return;
        }

        // 事件推送必须验签，不然谁都能伪造事件打进来
        if (obj.OpCode == OpDispatch && !CheckSignature(ctx, instance.BotSecret, ms.ToArray()))
        {
            ProxyInstance.Logger.Warn(ProxyLang.GetLang(ProxyLangKey.CallbackSignatureFail));

            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        // 平台只认 HTTP 200，空响应体；先回掉，插件跑得慢也不能让平台超时重推
        await WriteOkAsync(ctx);

        if (obj.OpCode == OpDispatch)
        {
            StartDispatch(instance, obj);
        }
    }

    /// <summary>
    /// 后台跑事件处理，避免占用 webhook 响应
    /// </summary>
    private static void StartDispatch(ProxyInstance instance, BaseMessageObj envelope)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await DispatchAsync(instance, envelope);
            }
            catch (Exception e)
            {
                ProxyInstance.Logger.Error(e, ProxyLang.GetLang(ProxyLangKey.SendError, envelope.Type));
            }
        });
    }

    /// <summary>
    /// 事件推送：转成通用消息事件交给插件，再把回复发回去
    /// </summary>
    private static async Task DispatchAsync(ProxyInstance instance, BaseMessageObj envelope)
    {
        var message = QQEventParser.Parse(instance.ProxyId, instance.Uuid, envelope);

        if (message is null)
        {
            ProxyInstance.Logger.Debug(ProxyLang.GetLang(ProxyLangKey.CallbackEvent, envelope.Type));
            return;
        }

        var replies = instance.Dispatch(message);

        foreach (var reply in replies)
        {
            await instance.Send(message, reply);
        }
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
            await WriteOkAsync(ctx);
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
            await WriteOkAsync(ctx);
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
    /// 校验平台签名：签名内容是 timestamp + 原始报文，公钥由机器人密钥派生
    /// </summary>
    /// <param name="ctx">http 上下文</param>
    /// <param name="botSecret">机器人密钥</param>
    /// <param name="body">原始请求体</param>
    private static bool CheckSignature(HttpContext ctx, string botSecret, byte[] body)
    {
        var signature = ctx.Request.Headers[SignatureHeader].ToString();
        var timestamp = ctx.Request.Headers[TimestampHeader].ToString();

        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp) || string.IsNullOrWhiteSpace(botSecret))
        {
            return false;
        }

        try
        {
            var sig = Convert.FromHexString(signature);
            var ts = Encoding.UTF8.GetBytes(timestamp);
            var message = new byte[ts.Length + body.Length];

            ts.CopyTo(message, 0);
            body.CopyTo(message, ts.Length);

            var publicKey = Ed25519.PublicKeyFromSeed(DeriveSeed(botSecret));

            return Ed25519.Verify(publicKey, message, sig);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// 由机器人密钥派生 32 字节种子：密钥循环补齐到 32 字节，不足补、超了截断
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
    /// 空响应：平台的事件推送只要求 HTTP 200
    /// </summary>
    /// <param name="ctx">http 上下文</param>
    private static async Task WriteOkAsync(HttpContext ctx)
    {
        if (ProxyMain.LogPayload)
        {
            ProxyInstance.Logger.Debug(ProxyLang.GetLang(ProxyLangKey.CallbackResponse, "HTTP 200 (empty)"));
        }

        ctx.Response.StatusCode = StatusCodes.Status200OK;
        await ctx.Response.CompleteAsync();
    }

    /// <summary>
    /// 写 json 响应，顺手记一条返回内容
    /// </summary>
    /// <param name="ctx">http 上下文</param>
    /// <param name="json">响应体</param>
    private static async Task WriteAsync(HttpContext ctx, string json)
    {
        // 调试开关打开时才记返回内容
        if (ProxyMain.LogPayload)
        {
            ProxyInstance.Logger.Debug(ProxyLang.GetLang(ProxyLangKey.CallbackResponse, json));
        }

        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(json, ctx.RequestAborted);
    }
}