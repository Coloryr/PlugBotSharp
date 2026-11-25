using PlugBotSharp.Core.Configs;

public static class Meta
{
    public static MetaObj Info = new()
    {
        Name = "GenshinProxy",
        Id = "genshin",
        Version = "1.0.0",
        Description = "gsuid_core 通信代理",
        Type = ComponentType.Proxy,
        Dlls =
        [
            "PlugBotSharp.GenshinProxy.dll"
        ],
        Deps = []
    };
}