using PlugBotSharp.Api;
using PlugBotSharp.Api.Configs;
using PlugBotSharp.Core.Components;

namespace PlugBotSharp.Tests;

/// <summary>
/// 直接往 ComponentManager 里放组件，避免测试依赖真实程序集
/// </summary>
internal static class TestComponents
{
    public static PluginAssembly AddPlugin(string id, IBasePlugin plugin, params (string Id, string Version)[] componentDeps)
    {
        return AddPlugin(id, "1.0.0", plugin, componentDeps);
    }

    public static PluginAssembly AddPlugin(string id, string version, IBasePlugin plugin, params (string Id, string Version)[] componentDeps)
    {
        var meta = new MetaObj
        {
            Id = id,
            Name = id,
            Version = version,
            Dlls = [],
            Deps = componentDeps
                .Select(d => new DepInfoObj { Type = DepType.Component, Id = d.Id, Version = d.Version })
                .ToList()
        };

        var assembly = new PluginAssembly(plugin)
        {
            Meta = meta,
            Folder = Path.GetTempPath()
        };

        ComponentManager.Plugins[id] = assembly;

        return assembly;
    }

    public static ProxyAssembly AddProxy(string id, IBaseProxy proxy)
    {
        var meta = new MetaObj
        {
            Id = id,
            Name = id,
            Version = "1.0.0",
            Dlls = [],
            Deps = []
        };

        var assembly = new ProxyAssembly(proxy)
        {
            Meta = meta,
            Folder = Path.GetTempPath()
        };

        ComponentManager.Proxys[id] = assembly;

        return assembly;
    }
}
