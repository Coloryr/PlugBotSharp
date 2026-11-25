using PlugBotSharp.QQLib;

namespace PlugBotSharp.QQProxy;

/// <summary>
/// 一次发送的结果，Data 为空时 Code 是平台错误码
/// </summary>
/// <param name="Data">平台返回的消息信息</param>
/// <param name="Code">失败时的平台错误码，0 表示没有</param>
public readonly record struct SendResult(SendGroupMessageResObj? Data, long Code)
{
    /// <summary>
    /// 是否发送成功
    /// </summary>
    public bool Ok => Data is not null;
}
