namespace PlugBotSharp.Core;

/// <summary>
/// 自定义事件的名字规则：[组件id]事件id
/// </summary>
public static class PluginEventName
{
    /// <summary>
    /// 拼出事件全名
    /// </summary>
    /// <param name="componentId">组件 id</param>
    /// <param name="eventId">事件 id</param>
    /// <returns>[组件id]事件id</returns>
    public static string Build(string componentId, string eventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        if (componentId.Contains('[') || componentId.Contains(']') || eventId.Contains('[') || eventId.Contains(']'))
        {
            throw new ArgumentException(BotLang.GetLang(BotLangKey.EventNameInvalid, $"[{componentId}]{eventId}"));
        }

        return $"[{componentId}]{eventId}";
    }

    /// <summary>
    /// 拆出组件 id 和事件 id
    /// </summary>
    /// <param name="name">事件全名</param>
    /// <param name="componentId">组件 id</param>
    /// <param name="eventId">事件 id</param>
    /// <returns>名字是否合法</returns>
    public static bool TryParse(string? name, out string componentId, out string eventId)
    {
        componentId = "";
        eventId = "";

        if (string.IsNullOrWhiteSpace(name) || name[0] != '[')
        {
            return false;
        }

        var end = name.IndexOf(']');

        if (end <= 1 || end == name.Length - 1)
        {
            return false;
        }

        componentId = name[1..end];
        eventId = name[(end + 1)..];

        return true;
    }
}
