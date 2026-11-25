using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.QQLib;

namespace PlugBotSharp.QQProxy;

internal static class QQApi
{
    public const string Url = "https://api.bot.qq.com/";

    /// <summary>
    /// 取凭证走另外的域名，和业务接口不是同一个
    /// </summary>
    public const string TokenUrl = "https://bots.qq.com/app/getAppAccessToken";

    /// <summary>
    /// Http客户端
    /// </summary>
    private readonly static HttpClient _httpClient = new();

    /// <summary>
    /// 获取访问凭证
    /// </summary>
    public static async Task<GetAppAccessTokenResObj?> GetToken(GetAppAccessTokenObj obj)
    {
        var res = await _httpClient.PostAsJsonAsync(TokenUrl, obj, QQJsonContext.Default.GetAppAccessTokenObj);

        return await res.Content.ReadFromJsonAsync(QQJsonContext.Default.GetAppAccessTokenResObj);
    }

    /// <summary>
    /// 发送消息
    /// </summary>
    /// <param name="token">访问凭证</param>
    /// <param name="target">群 openid 或用户 openid</param>
    /// <param name="scene">会话类型</param>
    /// <param name="message">通用消息</param>
    /// <param name="replyId">被动回复的消息 id，主动发送传 null</param>
    /// <param name="sequence">回复序号</param>
    public static Task<SendResult> Send(string token, string target, MessageScene scene,
        BaseMessage message, string? replyId, int sequence)
    {
        return SendItem(token, target, scene, message, replyId, sequence, QQMessage.FindReplyId(message));
    }

    /// <summary>
    /// 发一条消息，图片先上传再按 file_info 发
    /// </summary>
    private static async Task<SendResult> SendItem(string token, string target, MessageScene scene,
        BaseMessage message, string? replyId, int sequence, string? quoted)
    {
        SendGroupMessageObj? obj;

        if (message is ImageMessage or ImageUrlMessage or ImageFileMessage)
        {
            var fileInfo = await UploadImage(token, target, scene, message);

            ProxyInstance.Logger.Debug($"上传 {message.Type} 结果: {(string.IsNullOrEmpty(fileInfo) ? "失败" : "ok")}");

            if (string.IsNullOrEmpty(fileInfo))
            {
                return new SendResult(null, 0);
            }

            obj = new SendGroupMessageObj
            {
                MsgType = 7,
                Content = "",
                Media = new MediaInfoObj { FileInfo = fileInfo }
            };
        }
        else
        {
            obj = QQMessage.From(message, replyId, null, sequence);
        }

        if (obj is null)
        {
            throw new NotSupportedException(message.Type.ToString());
        }

        obj.MsgId = replyId;
        obj.MsgSeq = sequence;

        if (!string.IsNullOrEmpty(quoted))
        {
            obj.MessageReference = new MessageReferenceObj { MessageId = quoted };
        }

        var (res, code) = await SendPayload(token, target, scene, obj);

        return new SendResult(res, code);
    }

    /// <summary>
    /// 按群聊 / 单聊发出去，带出错误码
    /// </summary>
    private static Task<(SendGroupMessageResObj? Data, long Code)> SendPayload(string token, string target,
        MessageScene scene, SendGroupMessageObj obj)
    {
        return scene == MessageScene.Group
            ? Post(token, $"v2/groups/{target}/messages", obj, QQJsonContext.Default.SendGroupMessageObj,
                QQJsonContext.Default.SendGroupMessageResObj)
            : Post(token, $"v2/users/{target}/messages", obj, QQJsonContext.Default.SendGroupMessageObj,
                QQJsonContext.Default.SendGroupMessageResObj);
    }

    /// <summary>
    /// 上传图片，返回发消息要用的 file_info
    /// </summary>
    private static async Task<string?> UploadImage(string token, string target, MessageScene scene, BaseMessage message)
    {
        var obj = new QQFileUploadObj { FileType = 1, SrvSendMsg = false };

        switch (message)
        {
            case ImageMessage image:
                obj.FileData = Convert.ToBase64String(image.Image.ToArray());
                break;
            case ImageUrlMessage image:
                obj.Url = image.Url;
                break;
            case ImageFileMessage image:
                if (File.Exists(image.File))
                {
                    obj.FileData = Convert.ToBase64String(await File.ReadAllBytesAsync(image.File));
                }
                else if (image.File.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    obj.Url = image.File;
                }

                break;
        }

        if (string.IsNullOrEmpty(obj.FileData) && string.IsNullOrEmpty(obj.Url))
        {
            ProxyInstance.Logger.Debug($"上传 {message.Type} 跳过: 没有内容");

            return null;
        }

        var path = scene == MessageScene.Group ? $"v2/groups/{target}/files" : $"v2/users/{target}/files";
        var (res, _) = await Post(token, path, obj, QQJsonContext.Default.QQFileUploadObj,
            QQJsonContext.Default.QQFileUploadResObj);

        return res?.FileInfo;
    }

    /// <summary>
    /// 发送群消息
    /// </summary>
    public static async Task<SendGroupMessageResObj?> SendGroupMessage(string token, string group, SendGroupMessageObj obj)
    {
        var (res, _) = await Post(token, $"v2/groups/{group}/messages", obj,
            QQJsonContext.Default.SendGroupMessageObj, QQJsonContext.Default.SendGroupMessageResObj);

        return res;
    }

    /// <summary>
    /// 发送单聊消息
    /// </summary>
    public static async Task<SendGroupMessageResObj?> SendC2CMessage(string token, string openid, SendGroupMessageObj obj)
    {
        var (res, _) = await Post(token, $"v2/users/{openid}/messages", obj,
            QQJsonContext.Default.SendGroupMessageObj, QQJsonContext.Default.SendGroupMessageResObj);

        return res;
    }

    /// <summary>
    /// 被动回复超次数 / 超时的错误码
    /// </summary>
    public const long PassiveLimitCode = 40034128;

    /// <summary>
    /// 通用 post，序列化和反序列化都走源生成
    /// </summary>
    private static async Task<(TRes? Data, long Code)> Post<TReq, TRes>(string token, string url, TReq obj,
        JsonTypeInfo<TReq> reqInfo, JsonTypeInfo<TRes> resInfo)
    {
        using var req = BuildRequest(token, url);
        req.Content = JsonContent.Create(obj, reqInfo);

        using var res = await _httpClient.SendAsync(req);

        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync();

            ProxyInstance.Logger.Warn(ProxyLang.GetLang(ProxyLangKey.ApiError, url, (int)res.StatusCode, body));

            return (default, ParseCode(body));
        }

        return (await res.Content.ReadFromJsonAsync(resInfo), 0);
    }

    /// <summary>
    /// 从错误响应里取平台错误码
    /// </summary>
    private static long ParseCode(string body)
    {
        try
        {
            return JsonNode.Parse(body)?["code"]?.GetValue<long>() ?? 0;
        }
        catch (Exception)
        {
            return 0;
        }
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
