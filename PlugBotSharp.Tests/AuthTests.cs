using PlugBotSharp.Core.Auth;

namespace PlugBotSharp.Tests;

/// <summary>
/// 账户、防爆破和登录会话
/// </summary>
public class AuthTests : ComponentTestBase
{
    /// <summary>
    /// 密码哈希能校验，错密码不行
    /// </summary>
    [Fact]
    public void Password_Hash_Roundtrip()
    {
        var hash = PasswordHasher.Hash("password123");

        Assert.True(PasswordHasher.Verify("password123", hash));
        Assert.False(PasswordHasher.Verify("password124", hash));
        Assert.False(PasswordHasher.Verify("password123", "not-a-hash"));
        Assert.False(PasswordHasher.Verify("password123", ""));

        // 同一个密码每次盐不同
        Assert.NotEqual(hash, PasswordHasher.Hash("password123"));
    }

    /// <summary>
    /// 初始账户只能创建一次，之后只能通过添加账户
    /// </summary>
    [Fact]
    public void Initial_Account_Only_Once()
    {
        Assert.False(AccountManager.IsInitialized);
        Assert.Equal(0, AccountManager.Count);

        Assert.True(AccountManager.TryCreateInitial("admin", "password123", out var account, out var error), error);
        Assert.NotNull(account);
        Assert.True(AccountManager.IsInitialized);
        Assert.Equal(1, AccountManager.Count);

        // 第二次初始化必须失败
        Assert.False(AccountManager.TryCreateInitial("other", "password123", out _, out error));
        Assert.True(error.Length > 0);
        Assert.Equal(1, AccountManager.Count);

        // 初始化之后可以正常添加
        Assert.True(AccountManager.TryAdd("guest", "password123", out var guest, out error), error);
        Assert.Equal(2, AccountManager.Count);
        Assert.Equal("guest", guest!.User);

        // 重复用户名不区分大小写
        Assert.False(AccountManager.TryAdd("Guest", "password123", out _, out error));
    }

    /// <summary>
    /// 用户名和密码的格式校验
    /// </summary>
    [Fact]
    public void Account_Validation()
    {
        Assert.True(AccountManager.TryCreateInitial("admin", "password123", out _, out _));

        Assert.False(AccountManager.TryAdd("ab", "password123", out _, out _));
        Assert.False(AccountManager.TryAdd("has space", "password123", out _, out _));
        Assert.False(AccountManager.TryAdd("okuser", "short", out _, out _));
        Assert.True(AccountManager.TryAdd("ok_user.1", "password123", out _, out _));
    }

    /// <summary>
    /// 登录校验
    /// </summary>
    [Fact]
    public void Verify_Login()
    {
        Assert.True(AccountManager.TryCreateInitial("admin", "password123", out var account, out _));

        Assert.True(AccountManager.Verify("admin", "password123", out var matched));
        Assert.Equal(account!.Id, matched!.Id);

        Assert.False(AccountManager.Verify("admin", "wrong-password", out _));
        Assert.False(AccountManager.Verify("nobody", "password123", out _));
    }

    /// <summary>
    /// 连续失败后锁定，成功后清零
    /// </summary>
    [Fact]
    public void Login_Guard_Locks_After_Failures()
    {
        var ip = "10.1.2.3";

        Assert.True(LoginGuard.Check("admin", ip, out _));

        for (var i = 0; i < 5; i++)
        {
            LoginGuard.Fail("admin", ip);
        }

        Assert.False(LoginGuard.Check("admin", ip, out var retry));
        Assert.True(retry > 0);

        // 账户锁定跟 ip 无关，换 ip 也还是锁的
        Assert.False(LoginGuard.Check("admin", "10.1.2.4", out _));

        // 其他用户名不受影响
        Assert.True(LoginGuard.Check("guest", "10.1.2.4", out _));

        LoginGuard.Reset("admin", ip);
        Assert.True(LoginGuard.Check("admin", ip, out _));
    }

    /// <summary>
    /// 会话的签发、校验和撤销
    /// </summary>
    [Fact]
    public void Session_Lifecycle()
    {
        Assert.True(AccountManager.TryCreateInitial("admin", "password123", out var account, out _));

        var token = SessionManager.Create(account!.Id, "127.0.0.1", "test-agent");

        Assert.True(SessionManager.TryValidate(token, out var session, out _));
        Assert.Equal(account.Id, session!.AccountId);
        Assert.Equal(1, SessionManager.Count(account.Id));
        Assert.Single(SessionManager.List());

        // 错误的 token 不能通过
        Assert.False(SessionManager.TryValidate("bad-token", out _, out _));

        SessionManager.Remove(token);
        Assert.False(SessionManager.TryValidate(token, out _, out _));
        Assert.Equal(0, SessionManager.Count(account.Id));
    }

    /// <summary>
    /// 改密码需要原密码，管理员重置不需要
    /// </summary>
    [Fact]
    public void Change_Password()
    {
        Assert.True(AccountManager.TryCreateInitial("admin", "password123", out var account, out _));

        Assert.False(AccountManager.TryChangePassword(account!.Id, "wrong", "newpassword", out var error));
        Assert.True(error.Length > 0);

        Assert.True(AccountManager.TryChangePassword(account.Id, "password123", "newpassword", out error), error);
        Assert.True(AccountManager.Verify("admin", "newpassword", out _));

        Assert.True(AccountManager.TryChangePassword(account.Id, null, "another123", out error), error);
        Assert.True(AccountManager.Verify("admin", "another123", out _));

        Assert.False(AccountManager.TryChangePassword(account.Id, null, "short", out error));
    }

    /// <summary>
    /// 不能删掉最后一个账户
    /// </summary>
    [Fact]
    public void Cannot_Remove_Last_Account()
    {
        Assert.True(AccountManager.TryCreateInitial("admin", "password123", out var account, out _));

        Assert.False(AccountManager.TryRemove(account!.Id, out var error));
        Assert.True(error.Length > 0);
        Assert.Equal(1, AccountManager.Count);
    }
}
