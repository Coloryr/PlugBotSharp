using PlugBotSharp.Core.Configs;

public static class Meta
{
    public static MetaObj Info = new()
    {
        Name = "QQLib",
        Id = "qqlib",
        Version = "1.0.0",
        Description = "QQ 机器人平台模型和消息转换",
        Type = ComponentType.Lib,
        Dlls =
        [
            "PlugBotSharp.QQLib.dll"
        ],
        Deps = []
    };
}
