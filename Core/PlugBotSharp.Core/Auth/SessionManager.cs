using System.Security.Cryptography;
using System.Text;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core.Auth;

/// <summary>
/// 登录会话管理：签发 cookie、校验、滑动续期、撤销，会话保存在 Configs/Sessions.json
/// </summary>
internal static class SessionManager
{
    /// <summary>
    /// 登录 cookie 名
    /// </summary>
    public const string CookieName = "pbs.sid";

    /// <summary>
    /// 单次登录有效期，每次访问会自动续期
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// 会话最长有效期，续期也不会超过
    /// </summary>
    private static readonly TimeSpan MaxLifetime = TimeSpan.FromDays(30);

    private const string FileName = "Sessions.json";

    private static readonly Lock s_lock = new();

    /// <summary>
    /// key 是 token 的 sha256，cookie 里的 token 不落盘
    /// </summary>
    private static readonly Dictionary<string, SessionObj> s_sessions = new(StringComparer.Ordinal);

    private static string s_file = "";
    private static bool s_loaded;

    /// <summary>
    /// 初始化会话文件
    /// </summary>
    /// <param name="configsPath">配置目录</param>
    public static void Init(string configsPath)
    {
        s_file = Path.Combine(configsPath, FileName);

        lock (s_lock)
        {
            s_sessions.Clear();
            s_loaded = false;
        }
    }

    /// <summary>
    /// 创建会话，返回写进 cookie 的 token
    /// </summary>
    /// <param name="accountId">账户 id</param>
    /// <param name="ip">客户端 ip</param>
    /// <param name="agent">客户端 UA</param>
    public static string Create(string accountId, string? ip, string? agent)
    {
        var token = NewToken();
        var now = DateTime.UtcNow;

        var session = new SessionObj
        {
            Id = HashToken(token),
            AccountId = accountId,
            Ip = ip ?? "",
            Agent = Trim(agent, 300),
            Created = now,
            LastSeen = now,
            Expires = now + Lifetime
        };

        lock (s_lock)
        {
            EnsureLoad();
            CleanupLocked(now);
            s_sessions[session.Id] = session;
            SaveLocked();
        }

        return token;
    }

    /// <summary>
    /// 校验 token，快过期时自动续期
    /// </summary>
    /// <param name="token">cookie 里的 token</param>
    /// <param name="session">对应会话</param>
    /// <param name="renewed">是否续期了，需要重写 cookie</param>
    public static bool TryValidate(string? token, out SessionObj? session, out bool renewed)
    {
        session = null;
        renewed = false;

        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var id = HashToken(token);
        var now = DateTime.UtcNow;

        lock (s_lock)
        {
            EnsureLoad();

            if (!s_sessions.TryGetValue(id, out session))
            {
                return false;
            }

            if (session.Expires <= now)
            {
                s_sessions.Remove(id);
                SaveLocked();
                session = null;
                return false;
            }

            session.LastSeen = now;

            if (session.Expires - now < Lifetime / 2)
            {
                var max = session.Created + MaxLifetime;
                var next = now + Lifetime;

                session.Expires = next > max ? max : next;
                renewed = true;

                if (session.Expires <= now)
                {
                    s_sessions.Remove(id);
                    SaveLocked();
                    session = null;
                    return false;
                }

                SaveLocked();
            }

            return true;
        }
    }

    /// <summary>
    /// 按 token 撤销会话
    /// </summary>
    /// <param name="token">cookie 里的 token</param>
    public static void Remove(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        lock (s_lock)
        {
            EnsureLoad();

            if (s_sessions.Remove(HashToken(token)))
            {
                SaveLocked();
            }
        }
    }

    /// <summary>
    /// 按会话 id 撤销
    /// </summary>
    /// <param name="id">会话 id，token 的 sha256</param>
    public static bool RemoveById(string id)
    {
        lock (s_lock)
        {
            EnsureLoad();

            if (!s_sessions.Remove(id))
            {
                return false;
            }

            SaveLocked();
            return true;
        }
    }

    /// <summary>
    /// 撤销某个账户的全部会话
    /// </summary>
    /// <param name="accountId">账户 id</param>
    /// <param name="keepId">保留的会话 id，可以为空</param>
    public static void RemoveAccount(string accountId, string? keepId = null)
    {
        lock (s_lock)
        {
            EnsureLoad();

            var removed = s_sessions
                .Where(x => x.Value.AccountId == accountId && x.Key != keepId)
                .Select(x => x.Key)
                .ToList();

            if (removed.Count == 0)
            {
                return;
            }

            foreach (var key in removed)
            {
                s_sessions.Remove(key);
            }

            SaveLocked();
        }
    }

    /// <summary>
    /// 撤销除当前会话以外的全部会话
    /// </summary>
    /// <param name="keepId">保留的会话 id</param>
    public static void RemoveOthers(string? keepId)
    {
        lock (s_lock)
        {
            EnsureLoad();

            var removed = s_sessions.Keys.Where(x => x != keepId).ToList();

            if (removed.Count == 0)
            {
                return;
            }

            foreach (var key in removed)
            {
                s_sessions.Remove(key);
            }

            SaveLocked();
        }
    }

    /// <summary>
    /// 全部会话，最近活跃的排前面
    /// </summary>
    public static List<SessionObj> List()
    {
        lock (s_lock)
        {
            EnsureLoad();
            CleanupLocked(DateTime.UtcNow);

            return [.. s_sessions.Values.OrderByDescending(x => x.LastSeen)];
        }
    }

    /// <summary>
    /// 某个账户的会话数量
    /// </summary>
    /// <param name="accountId">账户 id</param>
    public static int Count(string accountId)
    {
        lock (s_lock)
        {
            EnsureLoad();
            return s_sessions.Values.Count(x => x.AccountId == accountId && x.Expires > DateTime.UtcNow);
        }
    }

    /// <summary>
    /// 写回会话文件，退出时调用
    /// </summary>
    public static void Save()
    {
        lock (s_lock)
        {
            EnsureLoad();
            SaveLocked();
        }
    }

    /// <summary>
    /// 清理过期会话
    /// </summary>
    private static void CleanupLocked(DateTime now)
    {
        var expired = s_sessions.Where(x => x.Value.Expires <= now).Select(x => x.Key).ToList();

        foreach (var key in expired)
        {
            s_sessions.Remove(key);
        }
    }

    /// <summary>
    /// 读取会话文件
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
            var obj = JsonUtils.ToObj(File.ReadAllText(s_file), SourceGenerationContext.Default.SessionFileObj);

            s_sessions.Clear();

            foreach (var item in obj?.Sessions ?? [])
            {
                if (!string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.AccountId))
                {
                    s_sessions[item.Id] = item;
                }
            }
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.ErrorConfigRead));
        }
    }

    /// <summary>
    /// 保存会话文件
    /// </summary>
    private static void SaveLocked()
    {
        try
        {
            var obj = new SessionFileObj { Sessions = [.. s_sessions.Values] };
            var text = JsonUtils.ToString(obj, SourceGenerationContext.Default.SessionFileObj);

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

    /// <summary>
    /// 新 token，32 字节随机数转 url 安全 base64
    /// </summary>
    private static string NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    /// token 的 sha256，十六进制
    /// </summary>
    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    /// <summary>
    /// 截断过长的字段
    /// </summary>
    private static string Trim(string? value, int max)
    {
        value ??= "";

        return value.Length <= max ? value : value[..max];
    }
}
