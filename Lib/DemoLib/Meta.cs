using PlugBotSharp.Core.Configs;

public static class Meta
{
    public static MetaObj Info = new()
    {
        Name = "DemoLib",
        Id = "demolib",
        Version = "1.0.0",
        Description = "示例运行库",
        Type = ComponentType.Lib,
        Dlls =
        [
            "DemoLib.dll"
        ],
        Deps = []
    };
}
