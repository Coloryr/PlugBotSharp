using System.Text.Json.Serialization;

namespace PlugBotSharp.QQLib;

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

public record MessageMarkdownObj
{
    /// <summary>
    /// Markdown 内容
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; }
    /// <summary>
    /// 是否校验图片转存结果
    /// </summary>
    [JsonPropertyName("force_verify_image_resource")]
    public bool ForceVerifyImageResource { get; set; }
}

public enum RenderDataStyle : int
{
    /// <summary>
    /// 灰色线框
    /// </summary>
    GrayBorder = 0,
    /// <summary>
    /// 蓝色线框
    /// </summary>
    BlueBorder = 1,
    /// <summary>
    /// 白色背景+红色字体
    /// </summary>
    WhiteBGRedFont = 2,
    /// <summary>
    /// 蓝色背景+白色字体
    /// </summary>
    BlueBGWhiteFont = 3
}

public record RenderDataObj
{
    /// <summary>
    /// 按钮文字，最多 10 字符
    /// </summary>
    [JsonPropertyName("label")]
    public string Label { get; set; }
    /// <summary>
    /// 点击后文字，不传则保持不变
    /// </summary>
    [JsonPropertyName("visited_label")]
    public string? VisitedLabel { get; set; }
    /// <summary>
    /// 按钮样式
    /// </summary>
    [JsonPropertyName("style")]
    public RenderDataStyle Style { get; set; }
}

public enum ActionType : int
{
    /// <summary>
    /// 跳转按钮：http 或 小程序
    /// </summary>
    JumpHttp = 0,
    /// <summary>
    /// 回调按钮：回调后台接口, data 传给后台
    /// </summary>
    CallBack = 1,
    /// <summary>
    /// 指令按钮：自动在输入框插入 @bot data
    /// </summary>
    Command = 2,
}

public enum PermissionType : int
{
    /// <summary>
    /// 指定用户
    /// </summary>
    User = 0,
    /// <summary>
    /// 管理员
    /// </summary>
    Admin = 1,
    /// <summary>
    /// 所有人
    /// </summary>
    All = 2,
}

public record PermissionObj
{
    /// <summary>
    /// 权限类型
    /// </summary>
    [JsonPropertyName("type")]
    public PermissionType Type { get; set; }
    /// <summary>
    /// 有权限的用户 id 的列表
    /// </summary>
    [JsonPropertyName("specify_user_ids")]
    public List<string>? SpecifyUserIds { get; set; }
    /// <summary>
    /// 有权限的身份组 id 的列表（仅频道可用）
    /// </summary>
    [JsonPropertyName("specify_role_ids")]
    public List<string>? SpecifyRoleIds { get; set; }
}

public record ModalObj
{
    /// <summary>
    /// 二次确认的提示文本,如果不为空则会进行二次确认. 注意:最多40个字符, 不能有URL
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; }
    /// <summary>
    /// 二次确认提示确认按钮中展示的文字,可以为空, 默认为"确认" 注意:最多4个字符
    /// </summary>
    [JsonPropertyName("confirm_text")]
    public string ConfirmText { get; set; }
    /// <summary>
    /// 二次确认提示取消按钮中的文字,可以为空,默认为"取消" 注意:最多4个字符
    /// </summary>
    [JsonPropertyName("cancel_text")]
    public string CancelText { get; set; }
}

public record ActionObj
{
    /// <summary>
    /// 指令类型
    /// </summary>
    [JsonPropertyName("style")]
    public ActionType Type { get; set; }
    /// <summary>
    /// 操作权限
    /// </summary>
    [JsonPropertyName("permission")]
    public PermissionObj? Permission { get; set; }
    /// <summary>
    /// 回调数据
    /// </summary>
    [JsonPropertyName("data")]
    public string? Data { get; set; }
    /// <summary>
    /// 版本过低时提示文案
    /// </summary>
    [JsonPropertyName("unsupport_tips")]
    public string? UnsupportTips { get; set; }
    /// <summary>
    /// 指令按钮可用，点击按钮后直接自动发送 data，仅单聊可用
    /// </summary>
    [JsonPropertyName("enter")]
    public bool Enter { get; set; }
    /// <summary>
    /// 指令按钮可用，指令是否带引用回复本消息
    /// </summary>
    [JsonPropertyName("reply")]
    public bool Reply { get; set; }
    /// <summary>
    /// 本字段仅在指令按钮下有效，设置后后会忽略 action.enter 配置。 设置为 1 时 ，点击按钮自动唤起启手Q选图器，其他值暂无效果。
    /// </summary>
    [JsonPropertyName("anchor")]
    public int Anchor { get; set; }
    /// <summary>
    /// 用户点击二次确认操作
    /// </summary>
    [JsonPropertyName("modal")]
    public ModalObj Modal { get; set; }
}

public record ButtonObj
{
    /// <summary>
    /// 按钮 ID
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }
    /// <summary>
    /// 分组ID
    /// </summary>
    [JsonPropertyName("group_id")]
    public string GroupId { get; set; }
    /// <summary>
    /// 按钮渲染
    /// </summary>
    [JsonPropertyName("render_data")]
    public RenderDataObj RenderData { get; set; }
    /// <summary>
    /// 按钮点击行为
    /// </summary>
    [JsonPropertyName("action")]
    public ActionObj Action { get; set; }
}

public record RowObj
{
    /// <summary>
    /// 行内按钮，从左到右排列
    /// </summary>
    [JsonPropertyName("buttons")]
    public List<ButtonObj> Buttons { get; set; }
}

public record KeyboardContentObj
{
    /// <summary>
    /// 按钮行列表
    /// </summary>
    [JsonPropertyName("rows")]
    public List<RowObj> Rows { get; set; }
}

public record KeyboardObj
{
    /// <summary>
    /// 内嵌键盘模板 ID
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    /// <summary>
    /// 自定义键盘布局
    /// </summary>
    [JsonPropertyName("content")]
    public KeyboardContentObj? Content { get; set; }
}
public record MediaInfoObj
{
    /// <summary>
    /// 文件数据
    /// </summary>
    [JsonPropertyName("file_info")]
    public string FileInfo { get; set; }
}

/// <summary>
/// 上传富媒体文件的请求
/// </summary>
public record QQFileUploadObj
{
    /// <summary>
    /// 文件类型，1 是图片
    /// </summary>
    [JsonPropertyName("file_type")]
    public int FileType { get; set; } = 1;
    /// <summary>
    /// 只上传不直接发送
    /// </summary>
    [JsonPropertyName("srv_send_msg")]
    public bool SrvSendMsg { get; set; }
    /// <summary>
    /// 文件内容，base64
    /// </summary>
    [JsonPropertyName("file_data")]
    public string? FileData { get; set; }
    /// <summary>
    /// 文件地址，和 file_data 二选一
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

/// <summary>
/// 上传富媒体文件的结果
/// </summary>
public record QQFileUploadResObj
{
    /// <summary>
    /// 文件信息，发消息时用
    /// </summary>
    [JsonPropertyName("file_info")]
    public string? FileInfo { get; set; }
    /// <summary>
    /// 过期时间
    /// </summary>
    [JsonPropertyName("ttl")]
    public int Ttl { get; set; }
    /// <summary>
    /// 文件 id
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    /// <summary>
    /// 错误码
    /// </summary>
    [JsonPropertyName("code")]
    public long? Code { get; set; }
    /// <summary>
    /// 错误信息
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public record MessageReferenceObj
{
    /// <summary>
    /// 被引用消息 ID
    /// </summary>
    [JsonPropertyName("message_id")]
    public string MessageId { get; set; }
}

public record CardObj
{
    /// <summary>
    /// 标题 1-50字符
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; }
    /// <summary>
    /// 描述 1-200字符
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; }
    /// <summary>
    /// 链接 https开头 不超过1024字符
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; }
    /// <summary>
    /// 图片链接 https开头 不超过1024字符
    /// </summary>
    [JsonPropertyName("pic_url")]
    public string PicUrl { get; set; }
}

public record MessageCardObj
{
    /// <summary>
    /// 卡片类型
    /// tuwen： 图文卡片
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "tuwen";
    [JsonPropertyName("content")]
    public CardObj Content { get; set; }
}

public record SendGroupMessageObj
{
    /// <summary>
    /// 消息类型
    /// </summary>
    [JsonPropertyName("msg_type")]
    public int MsgType { get; set; }
    /// <summary>
    /// 被动回复的消息 ID
    /// </summary>
    [JsonPropertyName("msg_id")]
    public string? MsgId { get; set; }
    /// <summary>
    /// 被动回复的事件 ID
    /// </summary>
    [JsonPropertyName("event_id")]
    public string? EventId { get; set; }
    /// <summary>
    /// 回复消息的序号
    /// </summary>
    [JsonPropertyName("msg_seq")]
    public int MsgSeq { get; set; }
    /// <summary>
    /// 引用回复
    /// </summary>
    [JsonPropertyName("message_reference")]
    public MessageReferenceObj? MessageReference { get; set; }
    /// <summary>
    /// 富媒体消息
    /// </summary>
    [JsonPropertyName("media")]
    public MediaInfoObj? Media { get; set; }
    /// <summary>
    /// 图文卡片
    /// </summary>
    [JsonPropertyName("card")]
    public MessageCardObj? Card { get; set; }
    /// <summary>
    /// 文本内容
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }
    /// <summary>
    /// Markdown 消息
    /// </summary>
    [JsonPropertyName("markdown")]
    public MessageMarkdownObj? Markdown { get; set; }
    /// <summary>
    /// 内嵌键盘
    /// </summary>
    [JsonPropertyName("keyboard")]
    public KeyboardObj? Keyboard { get; set; }
}

public record SendGroupMessageResObj
{
    public record MessageExtInfoObj
    {
        [JsonPropertyName("ext_info")]
        public string RefIdx { get; set; }
    }

    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("timestamp")]
    public string TimeStamp { get; set; }
    [JsonPropertyName("ext_info")]
    public MessageExtInfoObj ExtInfo { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GetAppAccessTokenObj))]
[JsonSerializable(typeof(GetAppAccessTokenResObj))]
[JsonSerializable(typeof(SendGroupMessageObj))]
[JsonSerializable(typeof(SendGroupMessageResObj))]
[JsonSerializable(typeof(QQGroupMessageObj))]
[JsonSerializable(typeof(QQFileUploadObj))]
[JsonSerializable(typeof(QQFileUploadResObj))]
[JsonSerializable(typeof(QQC2CMessageObj))]
public partial class QQJsonContext : JsonSerializerContext
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
