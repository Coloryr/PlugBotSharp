using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core.Http;

/// <summary>
/// 面板接口的公共响应和请求读取
/// </summary>
internal static class ApiUtil
{
    /// <summary>
    /// 成功响应
    /// </summary>
    /// <param name="data">数据</param>
    /// <param name="message">提示</param>
    public static IResult Ok(object? data = null, string message = "")
    {
        return Results.Json(new { ok = true, message, data });
    }

    /// <summary>
    /// 失败响应
    /// </summary>
    /// <param name="message">原因</param>
    /// <param name="status">状态码</param>
    public static IResult Fail(string message, int status = StatusCodes.Status400BadRequest)
    {
        return Results.Json(new { ok = false, message, data = (object?)null }, statusCode: status);
    }

    /// <summary>
    /// 读取 json 请求体，读不到或解析失败返回 null
    /// </summary>
    /// <param name="ctx">请求上下文</param>
    /// <param name="info">类型信息</param>
    public static async Task<T?> Read<T>(HttpContext ctx, JsonTypeInfo<T> info)
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var text = await reader.ReadToEndAsync(ctx.RequestAborted);

        if (string.IsNullOrWhiteSpace(text))
        {
            return default;
        }

        try
        {
            return JsonUtils.ToObj(text, info);
        }
        catch
        {
            return default;
        }
    }
}
