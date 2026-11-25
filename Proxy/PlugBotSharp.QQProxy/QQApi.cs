using System;
using System.Net.Http.Json;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.QQLib;

namespace PlugBotSharp.QQProxy;

internal static class QQApi
{
    public const string Url = "https://api.bot.qq.com/";

    /// <summary>
    /// Http客户端
    /// </summary>
    private readonly static HttpClient _httpClient = new();

    /// <summary>
    /// 获取访问凭证
    /// </summary>
    public static async Task<GetAppAccessTokenResObj?> GetToken(GetAppAccessTokenObj obj)
    {
        var res = await _httpClient.PostAsJsonAsync($"{Url}app/getAppAccessToken", obj, QQJsonContext.Default.GetAppAccessTokenObj);

        return await res.Content.ReadFromJsonAsync(QQJsonContext.Default.GetAppAccessTokenResObj);
    }

    /// <summary>
    /// 发送消息
    /// </summary>
    /// <param name="token">访问凭证</param>
    /// <param name="target">群 openid 或用户 openid</param>
    /// <param name="scene">会话类型</param>
    /// <param name="message">通用消息</param>
    /// <param name="replyId">被动回复的消息 id</param>
    /// <param name="sequence">回复序号</param>
    public static Task<SendGroupMessageResObj?> Send(string token, string target, MessageScene scene,
        BaseMessage message, string? replyId, int sequence)
    {
        var obj = QQMessage.From(message, replyId, null, sequence)
            ?? throw new NotSupportedException(message.Type.ToString());

        return scene == MessageScene.Group
            ? SendGroupMessage(token, target, obj)
            : SendC2CMessage(token, target, obj);
    }

    /// <summary>
    /// 发送群消息
    /// </summary>
    public static Task<SendGroupMessageResObj?> SendGroupMessage(string token, string group, SendGroupMessageObj obj)
    {
        return Post(token, $"v2/groups/{group}/messages", obj);
    }

    /// <summary>
    /// 发送单聊消息
    /// </summary>
    public static Task<SendGroupMessageResObj?> SendC2CMessage(string token, string openid, SendGroupMessageObj obj)
    {
        return Post(token, $"v2/users/{openid}/messages", obj);
    }

    /// <summary>
    /// 发消息的公共流程
    /// </summary>
    private static async Task<SendGroupMessageResObj?> Post(string token, string url, SendGroupMessageObj obj)
    {
        using var req = BuildRequest(token, url);
        req.Content = JsonContent.Create(obj, QQJsonContext.Default.SendGroupMessageObj);

        using var res = await _httpClient.SendAsync(req);

        if (!res.IsSuccessStatusCode)
        {
            return null;
        }

        return await res.Content.ReadFromJsonAsync(QQJsonContext.Default.SendGroupMessageResObj);
    }

    /// <summary>
    /// 组请求，带上 QQBot 凭证
    /// </summary>
    private static HttpRequestMessage BuildRequest(string token, string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, Url + url);

        req.Headers.Add("Authorization", $"QQBot {token}");

        return req;
    }
}
