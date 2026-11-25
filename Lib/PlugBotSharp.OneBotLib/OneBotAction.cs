using System.Text.Json.Nodes;

namespace PlugBotSharp.OneBotLib;

/// <summary>
/// 各版本调 API 时用的 action 和参数
/// </summary>
public static class OneBotAction
{
    /// <summary>
    /// 组发消息的请求
    /// </summary>
    /// <param name="version">版本</param>
    /// <param name="group">是不是群聊</param>
    /// <param name="target">群号或者用户 id</param>
    /// <param name="message">消息段</param>
    /// <returns>action 和参数</returns>
    public static (string Action, JsonObject Params) SendMessage(OneBotVersion version, bool group, string target, JsonArray message)
    {
        if (version == OneBotVersion.V12)
        {
            // v12 统一走 send_message，用 detail_type 区分会话
            return ("send_message", new JsonObject
            {
                ["detail_type"] = group ? "group" : "private",
                [group ? "group_id" : "user_id"] = target,
                ["message"] = message
            });
        }

        return (group ? "send_group_msg" : "send_private_msg", new JsonObject
        {
            [group ? "group_id" : "user_id"] = long.TryParse(target, out var id) ? id : 0,
            ["message"] = message,
            ["auto_escape"] = false
        });
    }
}
