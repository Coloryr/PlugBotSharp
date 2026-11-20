using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PlugBotSharp.QQProxy;

public enum HookType
{
    /// <summary>
    /// Http服务器注册到Core中
    /// </summary>
    WebHook,
    /// <summary>
    /// 使用WebSocket通信
    /// </summary>
    WebSocket,
    /// <summary>
    /// 自己启动Http服务器
    /// </summary>
    WebHost
}

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
    /// <summary>
    /// 通信类型
    /// </summary>
    public HookType Type { get; set; }
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

public record BaseMessageObj
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("op")]
    public int OpCode { get; set; }
    [JsonPropertyName("s")]
    public long Serial { get; set; }
    [JsonPropertyName("t")]
    public string Type { get; set; }
    [JsonPropertyName("d")]
    public JsonObject Data { get; set; }
}

/// <summary>
/// 回调地址验证请求，op 13 的 d 字段
/// </summary>
public record ValidationRequestObj
{
    /// <summary>
    /// 平台下发的明文 token
    /// </summary>
    [JsonPropertyName("plain_token")]
    public string PlainToken { get; set; }
    /// <summary>
    /// 事件时间戳
    /// </summary>
    [JsonPropertyName("event_ts")]
    public string EventTs { get; set; }
}

/// <summary>
/// 回调地址验证响应
/// </summary>
public record ValidationResponseObj
{
    /// <summary>
    /// 原样返回的明文 token
    /// </summary>
    [JsonPropertyName("plain_token")]
    public string PlainToken { get; set; }
    /// <summary>
    /// 签名，hex 小写
    /// </summary>
    [JsonPropertyName("signature")]
    public string Signature { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(InstanceDataObj))]
[JsonSerializable(typeof(GetAppAccessTokenObj))]
[JsonSerializable(typeof(GetAppAccessTokenResObj))]
[JsonSerializable(typeof(BaseMessageObj))]
[JsonSerializable(typeof(ValidationRequestObj))]
[JsonSerializable(typeof(ValidationResponseObj))]
internal partial class JsonContext : JsonSerializerContext
{

}

public static class EventNames
{
    /// <summary>
    /// 当机器人加入新guild时
    /// </summary>
    public const string GuildCreate = "GUILD_CREATE";
    /// <summary>
    /// 当guild资料发生变更时
    /// </summary>
    public const string GuildUpdate = "GUILD_UPDATE";
    /// <summary>
    /// 当机器人退出guild时
    /// </summary>
    public const string GuildDelete = "GUILD_DELETE";
    /// <summary>
    /// 当channel被创建时
    /// </summary>
    public const string GuildChannelCreate = "CHANNEL_CREATE";
    /// <summary>
    /// 当channel被更新时
    /// </summary>
    public const string GuildChannelUpdate = "CHANNEL_UPDATE";
    /// <summary>
    /// 当channel被删除时
    /// </summary>
    public const string GuildChannelDelete = "CHANNEL_DELETE";

    /// <summary>
    /// 当成员加入时
    /// </summary>
    public const string GuildMemberAdd = "GUILD_MEMBER_ADD";
    /// <summary>
    /// 当成员资料变更时
    /// </summary>
    public const string GuildMemberUpdate = "GUILD_MEMBER_UPDATE";
    /// <summary>
    /// 当成员被移除时
    /// </summary>
    public const string GuildMemberRemove = "GUILD_MEMBER_REMOVE";

    /// <summary>
    /// 发送消息事件，代表频道内的全部消息，而不只是 at 机器人的消息。内容与 AT_MESSAGE_CREATE 相同
    /// </summary>
    public const string GuildMessageCreate = "MESSAGE_CREATE";
    /// <summary>
    /// 删除（撤回）消息事件
    /// </summary>
    public const string GuildMessageDelete = "MESSAGE_DELETE";

    /// <summary>
    /// 为消息添加表情表态
    /// </summary>
    public const string GuildMessageReactionAdd = "MESSAGE_REACTION_ADD";
    /// <summary>
    /// 为消息删除表情表态
    /// </summary>
    public const string GuildMessageReactionRemove = "MESSAGE_REACTION_REMOVE";

    /// <summary>
    /// 当收到用户发给机器人的私信消息时
    /// </summary>
    public const string UserMessageCreate = "DIRECT_MESSAGE_CREATE";
    /// <summary>
    /// 删除（撤回）消息事件
    /// </summary>
    public const string UserMessageDelete = "DIRECT_MESSAGE_DELETE";

    /// <summary>
    /// 用户单聊发消息给机器人时候
    /// </summary>
    public const string GroupMessageCreate = "C2C_MESSAGE_CREATE";
    /// <summary>
    /// 用户添加使用机器人
    /// </summary>
    public const string FriendAdd = "FRIEND_ADD";
    /// <summary>
    /// 用户删除机器人
    /// </summary>
    public const string FriendDelete = "FRIEND_DEL";
    /// <summary>
    /// 用户在机器人资料卡手动关闭"主动消息"推送
    /// </summary>
    public const string FriendMessageOff = "C2C_MSG_REJECT";
    /// <summary>
    /// 用户在机器人资料卡手动开启"主动消息"推送开关
    /// </summary>
    public const string FriendMessageOn = "C2C_MSG_RECEIVE";
    /// <summary>
    /// 用户在群里@机器人时收到的消息
    /// </summary>
    public const string GroupAtMessageCreate = "GROUP_AT_MESSAGE_CREATE";
    /// <summary>
    /// 机器人被添加到群聊
    /// </summary>
    public const string GroupAdd = "GROUP_ADD_ROBOT";
    /// <summary>
    /// 机器人被移出群聊
    /// </summary>
    public const string GroupRemove = "GROUP_DEL_ROBOT";
    /// <summary>
    /// 群管理员主动在机器人资料页操作关闭通知
    /// </summary>
    public const string GroupMessageOff = "GROUP_MSG_REJECT";
    /// <summary>
    /// 群管理员主动在机器人资料页操作开启通知
    /// </summary>
    public const string GroupMessageOn = "GROUP_MSG_RECEIVE";

    /// <summary>
    /// 互动事件创建时
    /// </summary>
    public const string InteractionCreate = "INTERACTION_CREATE";

    /// <summary>
    /// 消息审核通过
    /// </summary>
    public const string MessageAuditPass = "MESSAGE_AUDIT_PASS";
    /// <summary>
    /// 消息审核通过
    /// </summary>
    public const string MessageAuditReject = "MESSAGE_AUDIT_REJECT";

    /// <summary>
    /// 当用户创建主题时
    /// </summary>
    public const string ForumThreadCreate = "FORUM_THREAD_CREATE";
    /// <summary>
    /// 当用户更新主题时
    /// </summary>
    public const string ForumThreadUpdate = "FORUM_THREAD_UPDATE";
    /// <summary>
    /// 当用户删除主题时
    /// </summary>
    public const string ForumThreadDelete = "FORUM_THREAD_DELETE";
    /// <summary>
    /// 当用户创建帖子时
    /// </summary>
    public const string ForumPostCreate = "FORUM_POST_CREATE";
    /// <summary>
    /// 当用户删除帖子时
    /// </summary>
    public const string ForumPostDelete = "FORUM_POST_DELETE";
    /// <summary>
    /// 当用户回复评论时
    /// </summary>
    public const string ForumReplyCreate = "FORUM_REPLY_CREATE";
    /// <summary>
    /// 当用户回复评论时
    /// </summary>
    public const string ForumReplyDelete = "FORUM_REPLY_DELETE";
    /// <summary>
    /// 当用户发表审核通过时
    /// </summary>
    public const string ForumPublishAuditResult = "FORUM_PUBLISH_AUDIT_RESULT";

    /// <summary>
    /// 音频开始播放时
    /// </summary>
    public const string AudioStart = "AUDIO_START";
    /// <summary>
    /// 音频播放结束时
    /// </summary>
    public const string AudioFinish = "AUDIO_FINISH";
    /// <summary>
    /// 上麦时
    /// </summary>
    public const string AudioMicOn = "AUDIO_ON_MIC";
    /// <summary>
    /// 下麦时
    /// </summary>
    public const string AudioMicOff = "AUDIO_OFF_MIC";

    /// <summary>
    /// 当收到@机器人的消息时
    /// </summary>
    public const string AtMessageCreate = "AT_MESSAGE_CREATE";
    /// <summary>
    /// 当频道的消息被删除时
    /// </summary>
    public const string MessageDelete = "PUBLIC_MESSAGE_DELETE";
}