namespace PlugBotSharp.Core;

/// <summary>
/// 证书清单（Certs/Certs.json）
/// </summary>
public class CertFileObj
{
    /// <summary>
    /// 证书列表
    /// </summary>
    public List<CertItemObj> Items { get; set; } = [];
}

/// <summary>
/// 单个证书
/// </summary>
public class CertItemObj
{
    /// <summary>
    /// 证书名，配置里填这个
    /// </summary>
    public string Name { get; set; } = "";
    /// <summary>
    /// pfx 文件名，相对 Certs 目录
    /// </summary>
    public string File { get; set; } = "";
    /// <summary>
    /// pfx 密码
    /// </summary>
    public string Password { get; set; } = "";
}
