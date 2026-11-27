using PlugBotSharp.Api.Configs;

namespace PlugBotSharp.Api;

/// <summary>
/// 组件信息，ComponentApi.GetComponents 返回
/// </summary>
public sealed record ComponentInfo
{
    /// <summary>
    /// 组件 id
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 显示名
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// 版本
    /// </summary>
    public string Version { get; init; } = "";

    /// <summary>
    /// 类型：插件 / 代理 / 运行库
    /// </summary>
    public ComponentType Type { get; init; }

    /// <summary>
    /// 说明
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// 组件目录
    /// </summary>
    public string Folder { get; init; } = "";

    /// <summary>
    /// 是否已加载到宿主
    /// </summary>
    public bool Loaded { get; init; }

    /// <summary>
    /// 是否已启用
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 依赖不满足，禁止启用
    /// </summary>
    public bool Blocked { get; init; }

    /// <summary>
    /// 展示状态：enabled / disabled / loaded / unloaded / blocked
    /// </summary>
    public string State { get; init; } = "";
}

/// <summary>
/// 实例信息，ComponentApi.GetInstances 返回
/// </summary>
/// <param name="Uuid">实例 uuid</param>
/// <param name="Proxy">所属代理组件 id</param>
/// <param name="Name">实例名称</param>
/// <param name="Folder">实例目录</param>
/// <param name="Loaded">是否已加载到宿主</param>
/// <param name="Enabled">是否已启用</param>
public sealed record InstanceInfo(Guid Uuid, string Proxy, string Name, string Folder, bool Loaded, bool Enabled);
