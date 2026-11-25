using PlugBotSharp.Core.Configs;

public static class Meta
{
    public static MetaObj Info = new()
    {
        Name = "OneBotLib",
        Id = "onebotlib",
        Version = "1.0.0",
        Description = "OneBot 消息段模型和 BaseMessage 转换",
        Type = ComponentType.Lib,
        Dlls =
        [
            "PlugBotSharp.OneBotLib.dll"
        ],
        Deps = []
    };
}
