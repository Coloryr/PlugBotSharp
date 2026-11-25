using PlugBotSharp.Core.Configs;

public static class Meta
{
    public static MetaObj Info = new()
    {
        Name = "OneBotProxy",
        Id = "onebot",
        Version = "1.0.0",
        Description = "OneBot 11 代理",
        Type = ComponentType.Proxy,
        Dlls =
        [
            "PlugBotSharp.OneBotProxy.dll"
        ],
        Deps =
        [
            new DepInfoObj { Type = DepType.Component, Id = "onebotlib", Version = ">=1.0.0" }
        ]
    };
}