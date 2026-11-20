using PlugBotSharp.Core.Configs;

public static class Meta
{
    public static MetaObj Info = new()
    {
        Name = "QQProxy",
        Id = "qq",
        Version = "1.0.0",
        Description = "官Q机器人",
        Type = ComponentType.Proxy,
        Dlls =
        [
            "PlugBotSharp.QQProxy.dll"
        ],
        Deps = []
    };
}
