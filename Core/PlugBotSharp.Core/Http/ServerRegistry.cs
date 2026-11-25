using System.Collections.Concurrent;

namespace PlugBotSharp.Core.Http;

/// <summary>
/// 组件服务器登记表，组件或实例结束时统一关掉
/// </summary>
internal static class ServerRegistry
{
    private static readonly ConcurrentDictionary<object, List<ComponentServer>> s_servers = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// 登记服务器
    /// </summary>
    public static void Add(object owner, ComponentServer server)
    {
        var list = s_servers.GetOrAdd(owner, _ => []);

        lock (list)
        {
            list.Add(server);
        }
    }

    /// <summary>
    /// 关掉某个 owner 的全部服务器
    /// </summary>
    /// <param name="owner">组件的插件 / 代理 / 实例对象</param>
    public static void StopAll(object owner)
    {
        if (!s_servers.TryRemove(owner, out var list))
        {
            return;
        }

        foreach (var server in list)
        {
            try
            {
                server.Stop();
            }
            catch (Exception e)
            {
                BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.HttpStopError));
            }
        }
    }
}
