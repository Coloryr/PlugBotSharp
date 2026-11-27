namespace PlugBotSharp.Core.Configs;

/// <summary>
/// 面板账户
/// </summary>
internal class AccountObj
{
    /// <summary>
    /// 账户 id，用于会话和接口
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// 登录名
    /// </summary>
    public string User { get; set; }

    /// <summary>
    /// 密码哈希，pbkdf2-sha256$迭代次数$salt$hash
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// 最后登录时间
    /// </summary>
    public DateTime LastLogin { get; set; }
}

/// <summary>
/// 账户文件 Configs/Accounts.json
/// </summary>
internal class AccountFileObj
{
    public List<AccountObj> Accounts { get; set; } = [];
}

/// <summary>
/// 登录会话，Id 是 cookie 里 token 的 sha256，token 本身不落盘
/// </summary>
internal class SessionObj
{
    public string Id { get; set; }
    public string AccountId { get; set; }
    public string Ip { get; set; }
    public string Agent { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastSeen { get; set; }
    public DateTime Expires { get; set; }
}

/// <summary>
/// 会话文件 Configs/Sessions.json
/// </summary>
internal class SessionFileObj
{
    public List<SessionObj> Sessions { get; set; } = [];
}

/// <summary>
/// 登录请求
/// </summary>
internal class LoginReqObj
{
    public string User { get; set; }
    public string Password { get; set; }
}

/// <summary>
/// 创建初始账户请求
/// </summary>
internal class InitAccountReqObj
{
    public string User { get; set; }
    public string Password { get; set; }
}

/// <summary>
/// 添加账户请求
/// </summary>
internal class AddAccountReqObj
{
    public string User { get; set; }
    public string Password { get; set; }
}

/// <summary>
/// 修改密码请求，OldPassword 为空表示管理员重置
/// </summary>
internal class ChangePasswordReqObj
{
    public string OldPassword { get; set; }
    public string Password { get; set; }
}
