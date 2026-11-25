using System.Reflection;

namespace PlugBotSharp.Core.Http;

/// <summary>
/// 编译时嵌入程序集的前端资源
/// </summary>
internal static class EmbeddedAssets
{
    /// <summary>
    /// 资源前缀，与 csproj 中的 LogicalName 对应
    /// </summary>
    private const string Prefix = "wwwroot/";

    private static readonly Dictionary<string, string> s_files = Build();
    private static readonly Dictionary<string, string> s_types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".mjs"] = "text/javascript; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".ico"] = "image/x-icon",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf",
        [".map"] = "application/json; charset=utf-8",
        [".txt"] = "text/plain; charset=utf-8",
    };

    /// <summary>
    /// 打开资源流
    /// </summary>
    /// <param name="path">相对路径，如 index.html、assets/index-xxx.js</param>
    public static Stream? Open(string path)
    {
        if (!s_files.TryGetValue(path, out var name))
        {
            return null;
        }

        return typeof(EmbeddedAssets).Assembly.GetManifestResourceStream(name);
    }

    /// <summary>
    /// 获取资源类型
    /// </summary>
    public static string GetContentType(string path)
    {
        return s_types.TryGetValue(Path.GetExtension(path), out var type) ? type : "application/octet-stream";
    }

    /// <summary>
    /// 扫描程序集清单资源，建立「路径 -> 资源名」映射
    /// </summary>
    private static Dictionary<string, string> Build()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in typeof(EmbeddedAssets).Assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            files[name[Prefix.Length..].Replace('\\', '/')] = name;
        }

        return files;
    }
}
