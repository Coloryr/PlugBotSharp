using System.Text.RegularExpressions;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core.Auth;

/// <summary>
/// 面板账户管理：初始化、登录校验、增删改，账户保存在 Configs/Accounts.json
/// </summary>
internal static class AccountManager
{
    private const string FileName = "Accounts.json";

    /// <summary>
    /// 用户名规则：3-32 位字母数字下划线点和横线
    /// </summary>
    private static readonly Regex s_userRule = new("^[A-Za-z0-9_.-]{3,32}$", RegexOptions.Compiled);

    /// <summary>
    /// 用户不存在时也做一次哈希，避免用响应时间判断账户是否存在
    /// </summary>
    private static readonly string s_dummyHash = PasswordHasher.Hash("plugbotsharp-dummy-password");

    private static readonly Lock s_lock = new();
    private static readonly List<AccountObj> s_accounts = [];

    private static string s_file = "";
    private static bool s_loaded;

    /// <summary>
    /// 初始化账户文件
    /// </summary>
    /// <param name="configsPath">配置目录</param>
    public static void Init(string configsPath)
    {
        s_file = Path.Combine(configsPath, FileName);

        lock (s_lock)
        {
            s_accounts.Clear();
            s_loaded = false;
        }
    }

    /// <summary>
    /// 是否已经创建过账户
    /// </summary>
    public static bool IsInitialized
    {
        get
        {
            lock (s_lock)
            {
                EnsureLoad();
                return s_accounts.Count > 0;
            }
        }
    }

    /// <summary>
    /// 账户数量
    /// </summary>
    public static int Count
    {
        get
        {
            lock (s_lock)
            {
                EnsureLoad();
                return s_accounts.Count;
            }
        }
    }

    /// <summary>
    /// 创建第一个账户，只有在没有任何账户时才能成功
    /// </summary>
    /// <param name="user">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="account">创建出来的账户</param>
    /// <param name="error">失败原因</param>
    public static bool TryCreateInitial(string user, string password, out AccountObj? account, out string error)
    {
        lock (s_lock)
        {
            EnsureLoad();

            if (s_accounts.Count > 0)
            {
                account = null;
                error = BotLang.GetLang(BotLangKey.AuthAlreadyInit);
                return false;
            }

            return TryAddLocked(user, password, out account, out error);
        }
    }

    /// <summary>
    /// 添加账户
    /// </summary>
    /// <param name="user">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="account">创建出来的账户</param>
    /// <param name="error">失败原因</param>
    public static bool TryAdd(string user, string password, out AccountObj? account, out string error)
    {
        lock (s_lock)
        {
            EnsureLoad();
            return TryAddLocked(user, password, out account, out error);
        }
    }

    /// <summary>
    /// 校验用户名密码
    /// </summary>
    /// <param name="user">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="account">匹配到的账户</param>
    public static bool Verify(string user, string password, out AccountObj? account)
    {
        user = (user ?? "").Trim();
        string? hash;

        lock (s_lock)
        {
            EnsureLoad();
            account = s_accounts.FirstOrDefault(x => string.Equals(x.User, user, StringComparison.OrdinalIgnoreCase));
            hash = account?.Password;
        }

        // 哈希放在锁外面算，避免登录时长时间占锁
        if (hash is null)
        {
            PasswordHasher.Verify(password ?? "", s_dummyHash);
            return false;
        }

        return PasswordHasher.Verify(password ?? "", hash);
    }

    /// <summary>
    /// 修改密码，OldPassword 为空表示管理员直接重置
    /// </summary>
    /// <param name="id">账户 id</param>
    /// <param name="oldPassword">原密码，可空</param>
    /// <param name="password">新密码</param>
    /// <param name="error">失败原因</param>
    public static bool TryChangePassword(string id, string? oldPassword, string password, out string error)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
        {
            error = BotLang.GetLang(BotLangKey.AuthPasswordWeak);
            return false;
        }

        lock (s_lock)
        {
            EnsureLoad();

            var account = s_accounts.FirstOrDefault(x => x.Id == id);

            if (account is null)
            {
                error = BotLang.GetLang(BotLangKey.AuthUserNotFound);
                return false;
            }

            if (!string.IsNullOrEmpty(oldPassword) && !PasswordHasher.Verify(oldPassword, account.Password))
            {
                error = BotLang.GetLang(BotLangKey.AuthOldPasswordWrong);
                return false;
            }

            account.Password = PasswordHasher.Hash(password);
            SaveLocked();

            error = "";
            return true;
        }
    }

    /// <summary>
    /// 删除账户，至少要保留一个
    /// </summary>
    /// <param name="id">账户 id</param>
    /// <param name="error">失败原因</param>
    public static bool TryRemove(string id, out string error)
    {
        lock (s_lock)
        {
            EnsureLoad();

            if (s_accounts.Count <= 1)
            {
                error = BotLang.GetLang(BotLangKey.AuthLastAccount);
                return false;
            }

            var account = s_accounts.FirstOrDefault(x => x.Id == id);

            if (account is null)
            {
                error = BotLang.GetLang(BotLangKey.AuthUserNotFound);
                return false;
            }

            s_accounts.Remove(account);
            SaveLocked();

            error = "";
            return true;
        }
    }

    /// <summary>
    /// 按 id 找账户
    /// </summary>
    /// <param name="id">账户 id</param>
    public static AccountObj? Find(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        lock (s_lock)
        {
            EnsureLoad();
            return s_accounts.FirstOrDefault(x => x.Id == id);
        }
    }

    /// <summary>
    /// 全部账户
    /// </summary>
    public static List<AccountObj> List()
    {
        lock (s_lock)
        {
            EnsureLoad();
            return [.. s_accounts.OrderBy(x => x.Created)];
        }
    }

    /// <summary>
    /// 记录最后登录时间
    /// </summary>
    /// <param name="account">账户</param>
    public static void TouchLogin(AccountObj account)
    {
        lock (s_lock)
        {
            account.LastLogin = DateTime.Now;
            SaveLocked();
        }
    }

    /// <summary>
    /// 新增账户，调用方已经持有锁
    /// </summary>
    private static bool TryAddLocked(string user, string password, out AccountObj? account, out string error)
    {
        account = null;
        user = (user ?? "").Trim();

        if (!s_userRule.IsMatch(user))
        {
            error = BotLang.GetLang(BotLangKey.AuthUserInvalid);
            return false;
        }

        if (string.IsNullOrEmpty(password) || password.Length < 8)
        {
            error = BotLang.GetLang(BotLangKey.AuthPasswordWeak);
            return false;
        }

        if (s_accounts.Any(x => string.Equals(x.User, user, StringComparison.OrdinalIgnoreCase)))
        {
            error = BotLang.GetLang(BotLangKey.AuthUserExists);
            return false;
        }

        account = new AccountObj
        {
            Id = Guid.NewGuid().ToString("N"),
            User = user,
            Password = PasswordHasher.Hash(password),
            Created = DateTime.Now
        };

        s_accounts.Add(account);
        SaveLocked();

        error = "";
        return true;
    }

    /// <summary>
    /// 第一次访问时读取账户文件
    /// </summary>
    private static void EnsureLoad()
    {
        if (s_loaded)
        {
            return;
        }

        s_loaded = true;

        if (!File.Exists(s_file))
        {
            return;
        }

        try
        {
            var obj = JsonUtils.ToObj(File.ReadAllText(s_file), SourceGenerationContext.Default.AccountFileObj);

            s_accounts.Clear();

            foreach (var item in obj?.Accounts ?? [])
            {
                if (!string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.User))
                {
                    s_accounts.Add(item);
                }
            }
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.ErrorConfigRead));
        }
    }

    /// <summary>
    /// 保存账户文件，先写临时文件再替换
    /// </summary>
    private static void SaveLocked()
    {
        try
        {
            var obj = new AccountFileObj { Accounts = [.. s_accounts] };
            var text = JsonUtils.ToString(obj, SourceGenerationContext.Default.AccountFileObj);

            Directory.CreateDirectory(Path.GetDirectoryName(s_file)!);

            var temp = s_file + ".tmp";
            File.WriteAllText(temp, text);
            File.Move(temp, s_file, true);
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.ErrorConfigSave));
        }
    }
}
