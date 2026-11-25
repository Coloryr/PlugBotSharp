namespace PlugBotSharp.Core.Configs;

/// <summary>
/// 实例启用状态记录
/// </summary>
internal class InstanceStateObj
{
    /// <summary>
    /// 实例启用状态：实例 id -> 是否启用
    /// </summary>
    public Dictionary<string, bool> Instances { get; set; } = [];
}
