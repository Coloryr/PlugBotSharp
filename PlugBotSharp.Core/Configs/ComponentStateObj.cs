namespace PlugBotSharp.Core.Configs;

/// <summary>
/// 组件启用状态记录
/// </summary>
internal class ComponentStateObj
{
    /// <summary>
    /// 组件启用状态：组件 id -> 是否启用
    /// </summary>
    public Dictionary<string, bool> Components { get; set; } = [];
}
