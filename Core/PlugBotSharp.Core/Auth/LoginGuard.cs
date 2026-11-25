using System.Collections.Concurrent;

namespace PlugBotSharp.Core.Auth;

/// <summary>
/// 登录防爆破：按账户和按 ip 统计失败次数，超过阈值锁定一段时间
/// </summary>
internal static class LoginGuard
{
    /// <summary>
    /// 单个账户允许的失败次数
    /// </summary>
    private const int UserLimit = 5;

    /// <summary>
    /// 单个 ip 允许的失败次数
    /// </summary>
    private const int IpLimit = 20;

    /// <summary>
    /// 失败次数统计窗口
    /// </summary>
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 账户锁定时长
    /// </summary>
    private static readonly TimeSpan UserLock = TimeSpan.FromMinutes(10);

    /// <summary>
    /// ip 锁定时长
    /// </summary>
    private static readonly TimeSpan IpLock = TimeSpan.FromMinutes(30);

    private sealed class Entry
    {
        public int Count { get; set; }
        public DateTime First { get; set; } = DateTime.UtcNow;
        public DateTime LockedUntil { get; set; }
    }

    private static readonly ConcurrentDictionary<string, Entry> s_users = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Entry> s_ips = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 是否允许这次登录尝试
    /// </summary>
    /// <param name="user">用户名</param>
    /// <param name="ip">客户端 ip</param>
    /// <param name="retryAfter">锁定时剩余秒数</param>
    public static bool Check(string user, string ip, out int retryAfter)
    {
        retryAfter = 0;

        var now = DateTime.UtcNow;

        return !IsLocked(s_users, "u:" + (user ?? "").ToLowerInvariant(), now, out retryAfter)
            && !IsLocked(s_ips, "ip:" + (ip ?? ""), now, out retryAfter);
    }

    /// <summary>
    /// 记录一次失败
    /// </summary>
    /// <param name="user">用户名</param>
    /// <param name="ip">客户端 ip</param>
    public static void Fail(string user, string ip)
    {
        var now = DateTime.UtcNow;

        Bump(s_users, "u:" + (user ?? "").ToLowerInvariant(), now, UserLimit, UserLock);
        Bump(s_ips, "ip:" + (ip ?? ""), now, IpLimit, IpLock);
    }

    /// <summary>
    /// 登录成功后清空计数
    /// </summary>
    /// <param name="user">用户名</param>
    /// <param name="ip">客户端 ip</param>
    public static void Reset(string user, string ip)
    {
        s_users.TryRemove("u:" + (user ?? "").ToLowerInvariant(), out _);
        s_ips.TryRemove("ip:" + (ip ?? ""), out _);
    }

    /// <summary>
    /// 检查某个 key 是否在锁定中
    /// </summary>
    private static bool IsLocked(ConcurrentDictionary<string, Entry> map, string key, DateTime now, out int retryAfter)
    {
        retryAfter = 0;

        if (!map.TryGetValue(key, out var entry))
        {
            return false;
        }

        lock (entry)
        {
            if (entry.LockedUntil <= now)
            {
                return false;
            }

            retryAfter = (int)Math.Ceiling((entry.LockedUntil - now).TotalSeconds);
            return true;
        }
    }

    /// <summary>
    /// 累加失败次数，达到阈值就锁定
    /// </summary>
    private static void Bump(ConcurrentDictionary<string, Entry> map, string key, DateTime now, int limit, TimeSpan lockFor)
    {
        var entry = map.GetOrAdd(key, _ => new Entry());

        lock (entry)
        {
            // 已经在锁定中，不重复计数
            if (entry.LockedUntil > now)
            {
                return;
            }

            // 超出统计窗口，重新计数
            if (now - entry.First > Window)
            {
                entry.First = now;
                entry.Count = 0;
            }

            entry.Count++;

            if (entry.Count >= limit)
            {
                entry.LockedUntil = now + lockFor;
                entry.Count = 0;
                entry.First = now;
            }
        }
    }
}
