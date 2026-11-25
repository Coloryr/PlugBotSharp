using PlugBotSharp.Core.Configs;

public static class Meta
{
    public static MetaObj Info = new()
    {
        Name = "ProxyDemo",
        Id = "proxydemo",
        Version = "1.0.0",
        Description = "示例代理，演示怎么把外部消息转给插件",
        Type = ComponentType.Proxy,
        Dlls =
        [
            "ProxyDemo.dll"
        ],
        Deps = []
    };
}
