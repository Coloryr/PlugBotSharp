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

    /// <summary>
    /// IIS 导出的 pfx 证书路径，填了就用 https，为空走 http
    /// </summary>
    public string Certificate { get; set; } = "";

    /// <summary>
    /// 证书密码
    /// </summary>
    public string CertificatePassword { get; set; } = "";
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

/// <summary>
/// 面板调试窗口注入的消息
/// </summary>
internal class DebugMessageObj
{
    /// <summary>
    /// 注入范围：空是全部，plugin:{组件id} 只发给插件，instance:{实例uuid} 只发给实例
    /// </summary>
    public string Scope { get; set; } = "";

    /// <summary>
    /// 会话类型：group / user
    /// </summary>
    public string SenderType { get; set; } = "user";

    /// <summary>
    /// 发送者
    /// </summary>
    public string Sender { get; set; } = "";

    /// <summary>
    /// 回复目标
    /// </summary>
    public string Target { get; set; } = "";

    /// <summary>
    /// 被动回复 / 引用的消息 id
    /// </summary>
    public string ReplyId { get; set; } = "";

    /// <summary>
    /// 文本内容
    /// </summary>
    public string Text { get; set; } = "";
}

/// <summary>
/// 证书清单（Certs/Certs.json）
/// </summary>
internal class CertFileObj
{
    public List<CertItemObj> Items { get; set; } = [];
}

/// <summary>
/// 单个证书
/// </summary>
internal class CertItemObj
{
    /// <summary>
    /// 证书名，配置里填这个
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// pfx 文件名，相对 Certs 目录
    /// </summary>
    public string File { get; set; }
    /// <summary>
    /// pfx 密码
    /// </summary>
    public string Password { get; set; }
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
[JsonSerializable(typeof(CertFileObj))]
[JsonSerializable(typeof(CertItemObj))]
[JsonSerializable(typeof(DebugMessageObj))]
internal partial class SourceGenerationContext : JsonSerializerContext
{

}

/// <summary>
/// 配置项定义的序列化，字段用 camelCase，和面板其他接口保持一致
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ConfigMap))]
[JsonSerializable(typeof(List<ConfigMap>))]
[JsonSerializable(typeof(Dictionary<string, ConfigMap>))]
internal partial class ConfigMapContext : JsonSerializerContext
{

}