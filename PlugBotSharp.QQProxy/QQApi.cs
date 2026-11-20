using System;
using System.Net.Http.Json;

namespace PlugBotSharp.QQProxy;

internal static class QQApi
{
    public const string Url = "https://api.bot.qq.com/";

    /// <summary>
    /// Http客户端
    /// </summary>
    private readonly static HttpClient _httpClient = new();

    public static async Task<GetAppAccessTokenResObj?> GetToken(GetAppAccessTokenObj obj)
    {
        var res = await _httpClient.PostAsJsonAsync($"{Url}app/getAppAccessToken", obj, JsonContext.Default.GetAppAccessTokenObj);
        return await res.Content.ReadFromJsonAsync(JsonContext.Default.GetAppAccessTokenResObj);
    }

    // public static async 
}
