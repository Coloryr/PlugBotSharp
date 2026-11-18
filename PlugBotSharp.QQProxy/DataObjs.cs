using System.Text.Json.Serialization;

namespace PlugBotSharp.QQProxy;

/// <summary>
/// 配置文件
/// </summary>
public record InstanceDataObj
{
    /// <summary>
    /// 机器人ID
    /// </summary>
    public string AppId { get; set; }
    /// <summary>
    /// 机器人密钥
    /// </summary>
    public string AppSecret { get; set; }

    /// <summary>
    /// Http服务器端口
    /// </summary>
    public ushort Port { get; set; }
}

/// <summary>
/// 获取访问Token
/// </summary>
public record GetAppAccessTokenObj
{
    /// <summary>
    /// 机器人ID
    /// </summary>
    [JsonPropertyName("appId")]
    public required string AppId { get; set; }
    /// <summary>
    /// 机器人密钥
    /// </summary>
    [JsonPropertyName("clientSecret")]
    public required string Clientecret { get; set; }
}

/// <summary>
/// 访问密钥获取结果
/// </summary>
public record GetAppAccessTokenResObj
{
    /// <summary>
    /// 访问密钥
    /// </summary>
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
    /// <summary>
    /// 生效时间
    /// </summary>
    [JsonPropertyName("expires_in")]
    public string? ExpiresIn { get; set; }
    /// <summary>
    /// 错误码
    /// </summary>
    [JsonPropertyName("code")]
    public long? Code { get; set; }
    /// <summary>
    /// 错误消息
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(InstanceDataObj))]
[JsonSerializable(typeof(GetAppAccessTokenObj))]
[JsonSerializable(typeof(GetAppAccessTokenResObj))]
internal partial class JsonContext : JsonSerializerContext
{

}