using PlugBotSharp.Core.Configs;

public static class Meta
{
    public static MetaObj Info = new()
    {
        Name = "DemoPlugin",
        Id = "demo",
        Version = "1.0.0",
        Description = "示例插件",
        Type = ComponentType.Plugin,
        Dlls =
        [
            "DemoPlugin.dll"
        ],
        Deps =
        [
            new DepInfoObj
            {
                Type = DepType.Component,
                Id = "demolib",
                Version = ">=1.0.0"
            },
            new DepInfoObj
            {
                Type = DepType.Component,
                Id = "qqlib",
                Version = ">=1.0.0"
            },
            new DepInfoObj
            {
                Type = DepType.Nuget,
                Id = "SkiaSharp",
                Version = "4.157.0"
            }
        ]
    };
}