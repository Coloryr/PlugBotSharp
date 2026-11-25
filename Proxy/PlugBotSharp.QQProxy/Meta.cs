using PlugBotSharp.Core.Configs;

public static class Meta
{
    public static MetaObj Info = new()
    {
        Name = "QQProxy",
        Id = "qqproxy",
        Version = "1.0.0",
        Description = "官Q机器人",
        Type = ComponentType.Proxy,
        Dlls =
        [
            "PlugBotSharp.QQProxy.dll"
        ],
        Deps =
        [
            new DepInfoObj { Type = DepType.Component, Id = "qqlib", Version = ">=1.0.0" }
        ]
    };
}