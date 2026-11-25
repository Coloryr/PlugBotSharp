using PlugBotSharp.Core.Configs;

public static class Meta
{
    public static MetaObj Info = new()
    {
        Name = "CorePlugin",
        Id = "core",
        Version = "1.0.0",
        Description = "核心库",
        Type = ComponentType.Lib,
        Dlls =
        [
            "PlugBotSharp.Core.Lib.dll"
        ],
        Deps = []
    };
}
