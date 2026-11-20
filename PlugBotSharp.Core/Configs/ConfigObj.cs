using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace PlugBotSharp.Core.Configs;

internal class ConfigObj
{
    /// <summary>
    /// 管理面板端口，为 0 时不启动
    /// </summary>
    public ushort Port { get; set; } = 10000;
}

/// <summary>
/// 通信实例（Instances/{id}/Instance.json）
/// </summary>
internal class InstanceObj
{
    /// <summary>
    /// 所属代理组件 id
    /// </summary>
    public string Proxy { get; set; }

    public string Id { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// 实例 uuid，为空时用目录名
    /// </summary>
    public Guid Uuid { get; set; }
}

/// <summary>
/// 面板安装组件请求，从本地路径安装时使用
/// </summary>
internal class AdminInstallObj
{
    /// <summary>
    /// 组件目录或者 zip 路径
    /// </summary>
    public string Path { get; set; }
}

/// <summary>
/// 面板创建实例请求
/// </summary>
internal class AdminInstanceObj
{
    /// <summary>
    /// 代理组件 id
    /// </summary>
    public string Proxy { get; set; }

    /// <summary>
    /// 实例名称
    /// </summary>
    public string Name { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ConfigObj))]
[JsonSerializable(typeof(InstanceObj))]
[JsonSerializable(typeof(MetaObj))]
[JsonSerializable(typeof(ComponentStateObj))]
[JsonSerializable(typeof(InstanceStateObj))]
[JsonSerializable(typeof(AdminInstallObj))]
[JsonSerializable(typeof(AdminInstanceObj))]
[JsonSerializable(typeof(AccountObj))]
[JsonSerializable(typeof(AccountFileObj))]
[JsonSerializable(typeof(SessionObj))]
[JsonSerializable(typeof(SessionFileObj))]
[JsonSerializable(typeof(LoginReqObj))]
[JsonSerializable(typeof(InitAccountReqObj))]
[JsonSerializable(typeof(AddAccountReqObj))]
[JsonSerializable(typeof(ChangePasswordReqObj))]
internal partial class SourceGenerationContext : JsonSerializerContext
{

}