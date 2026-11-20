using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

namespace PlugBotSharp.Core.Http;

/// <summary>
/// 注册的 http 接口
/// </summary>
internal sealed class ApiEntry
{
    /// <summary>
    /// 注册者：IBasePlugin / IBaseProxy / IProxyInstasnce
    /// </summary>
    public required object Owner { get; init; }

    /// <summary>
    /// 回调
    /// </summary>
    public required Action<HttpContext> Call { get; init; }
}

/// <summary>
/// 组件/实例的 http 接口注册表，路径不带开头的斜杠，例如 api/qq
/// </summary>
internal static class ApiRegistry
{
    private static readonly ConcurrentDictionary<string, ApiEntry> s_apis = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 注册接口，同一个路径重复注册会覆盖
    /// </summary>
    /// <param name="path">接口路径，例如 api/qq</param>
    /// <param name="api">注册项</param>
    public static void Register(string path, ApiEntry api)
    {
        s_apis[path] = api;

        BotManager.Logger?.Info(BotLang.GetLang(BotLangKey.ApiRegistered, "/" + path));
    }

    /// <summary>
    /// 移除某个注册者的所有接口
    /// </summary>
    /// <param name="owner">IBasePlugin / IBaseProxy / IProxyInstasnce</param>
    public static void Unregister(object owner)
    {
        foreach (var pair in s_apis)
        {
            if (ReferenceEquals(pair.Value.Owner, owner) && s_apis.TryRemove(pair.Key, out _))
            {
                BotManager.Logger?.Info(BotLang.GetLang(BotLangKey.ApiRemoved, "/" + pair.Key));
            }
        }
    }

    /// <summary>
    /// 取接口回调
    /// </summary>
    public static bool TryGet(string path, out Action<HttpContext>? call)
    {
        if (s_apis.TryGetValue(path, out var api))
        {
            call = api.Call;
            return true;
        }

        call = null;
        return false;
    }
}
