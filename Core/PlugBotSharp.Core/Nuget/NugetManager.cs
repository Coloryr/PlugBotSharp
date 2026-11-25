using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core.Nuget;

/// <summary>
/// NuGet 依赖下载、解压与加载
/// </summary>
internal static class NugetManager
{
    private const string PackageUrl = "https://api.nuget.org/v3-flatcontainer/{0}/{1}/{0}.{1}.nupkg";

    private static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private static readonly Dictionary<string, string> s_assemblies = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> s_natives = new(StringComparer.OrdinalIgnoreCase);
    private static readonly NugetLoadContext s_context = new(s_assemblies);
    private static readonly Lock s_lock = new();

    private static string s_root = "";

    /// <summary>
    /// 初始化，指定 Nugets 目录
    /// </summary>
    public static void Init(string root)
    {
        s_root = root;
    }

    /// <summary>
    /// 扫描目录下已安装的包，建立程序集和 native 库索引
    /// </summary>
    public static void Scan()
    {
        if (!Directory.Exists(s_root))
        {
            return;
        }

        foreach (var libDir in Directory.EnumerateDirectories(s_root, "lib", SearchOption.AllDirectories))
        {
            Register(libDir);
            RegisterNative(Path.GetDirectoryName(libDir)!);
        }
    }

    /// <summary>
    /// 确保组件声明的 NuGet 依赖都可用
    /// </summary>
    public static bool Ensure(MetaObj meta)
    {
        foreach (var dep in meta.Deps ?? [])
        {
            if (dep.Type != DepType.Nuget)
            {
                continue;
            }

            if (!Install(dep.Id, dep.Version))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 安装指定版本的包，已安装时直接复用
    /// </summary>
    public static bool Install(string id, string version)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var dir = GetPackageDir(id, version);
        var marker = Path.Combine(dir, ".installed");

        lock (s_lock)
        {
            if (File.Exists(marker))
            {
                Register(Path.Combine(dir, "lib"));
                RegisterNative(dir);
                return true;
            }
        }

        string? nupkg = null;

        try
        {
            BotManager.Logger?.Info(BotLang.GetLang(BotLangKey.NugetDownloading, id, version));

            nupkg = Download(id, version);

            if (nupkg is null)
            {
                return false;
            }

            lock (s_lock)
            {
                Directory.CreateDirectory(dir);
            }

            if (!Extract(nupkg, dir, id, version))
            {
                return false;
            }

            File.WriteAllText(marker, id + " " + version);

            Register(Path.Combine(dir, "lib"));
            RegisterNative(dir);

            BotManager.Logger?.Info(BotLang.GetLang(BotLangKey.NugetInstalled, id, version));

            return true;
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.NugetDownloadFail, id, version, e.Message));
            return false;
        }
        finally
        {
            if (nupkg is not null && File.Exists(nupkg))
            {
                File.Delete(nupkg);
            }
        }
    }

    /// <summary>
    /// 从已安装的包里加载程序集
    /// </summary>
    public static Assembly? Load(AssemblyName name)
    {
        if (name.Name is null)
        {
            return null;
        }

        lock (s_lock)
        {
            if (!s_assemblies.ContainsKey(name.Name))
            {
                return null;
            }
        }

        return s_context.LoadFromAssemblyName(new AssemblyName(name.Name));
    }

    /// <summary>
    /// 从已安装的包里找 native 库
    /// </summary>
    /// <param name="fileName">文件名，例如 libSkiaSharp.dll</param>
    /// <param name="path">找到的路径</param>
    public static bool TryResolveNative(string fileName, out string path)
    {
        lock (s_lock)
        {
            if (s_natives.TryGetValue(fileName, out var found))
            {
                path = found;
                return true;
            }
        }

        path = "";
        return false;
    }

    private static string? Download(string id, string version)
    {
        var lowerId = id.ToLowerInvariant();
        var lowerVersion = version.ToLowerInvariant();
        var url = string.Format(PackageUrl, lowerId, lowerVersion);

        using var response = s_http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();

        if (!response.IsSuccessStatusCode)
        {
            BotManager.Logger?.Error(BotLang.GetLang(BotLangKey.NugetDownloadFail, id, version, (int)response.StatusCode));
            return null;
        }

        var temp = Path.Combine(Path.GetTempPath(), "plugbotsharp-" + Guid.NewGuid().ToString("N") + ".nupkg");

        using (var file = File.Create(temp))
        {
            response.Content.CopyToAsync(file).GetAwaiter().GetResult();
        }

        return temp;
    }

    /// <summary>
    /// 解压 nupkg 中最合适的 lib 目录和 native 库
    /// </summary>
    private static bool Extract(string nupkg, string dir, string id, string version)
    {
        using var archive = ZipFile.OpenRead(nupkg);

        var entries = archive.Entries
            .Where(e => e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)
                     && e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var extracted = false;

        if (entries.Count > 0)
        {
            var group = entries
                .GroupBy(e => GetTfm(e.FullName))
                .OrderByDescending(g => ScoreTfm(g.Key))
                .First();

            var libDir = Path.Combine(dir, "lib", group.Key);
            Directory.CreateDirectory(libDir);

            foreach (var entry in group)
            {
                var target = Path.Combine(libDir, entry.Name);
                entry.ExtractToFile(target, true);
            }

            extracted = true;
        }

        // native 包只有 runtimes，没有 lib
        if (ExtractNative(archive, dir))
        {
            extracted = true;
        }

        if (!extracted)
        {
            BotManager.Logger?.Error(BotLang.GetLang(BotLangKey.NugetNoLib, id, version));
            return false;
        }

        return true;
    }

    /// <summary>
    /// 解压 runtimes/{rid}/native 下的 native 库
    /// </summary>
    /// <returns>是否解压出了文件</returns>
    private static bool ExtractNative(ZipArchive archive, string dir)
    {
        var extracted = false;

        var entries = archive.Entries
            .Where(e => e.FullName.StartsWith("runtimes/", StringComparison.OrdinalIgnoreCase)
                     && e.FullName.Contains("/native/", StringComparison.OrdinalIgnoreCase)
                     && e.Name.Length > 0);

        foreach (var entry in entries)
        {
            var target = Path.Combine(dir, entry.FullName.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, true);

            extracted = true;
        }

        return extracted;
    }

    /// <summary>
    /// 把 lib 目录下的 dll 注册到程序集索引
    /// </summary>
    private static void Register(string libRoot)
    {
        if (!Directory.Exists(libRoot))
        {
            return;
        }

        lock (s_lock)
        {
            foreach (var file in Directory.EnumerateFiles(libRoot, "*.dll", SearchOption.AllDirectories))
            {
                s_assemblies[Path.GetFileNameWithoutExtension(file)] = file;
            }
        }
    }

    /// <summary>
    /// 把包里的 native 库注册到索引，按当前 RID 优先
    /// </summary>
    private static void RegisterNative(string packageDir)
    {
        var runtimes = Path.Combine(packageDir, "runtimes");

        if (!Directory.Exists(runtimes))
        {
            return;
        }

        lock (s_lock)
        {
            foreach (var file in Directory.EnumerateFiles(runtimes, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(file);

                if (!s_natives.TryGetValue(name, out var current) || ScoreRid(file) > ScoreRid(current))
                {
                    s_natives[name] = file;
                }
            }
        }
    }

    /// <summary>
    /// 给 native 库路径打分，当前 RID 的优先级最高
    /// </summary>
    private static int ScoreRid(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var index = Array.FindIndex(parts, p => p.Equals("runtimes", StringComparison.OrdinalIgnoreCase));

        if (index < 0 || index + 1 >= parts.Length)
        {
            return 0;
        }

        var rid = parts[index + 1];

        if (rid.Equals(RuntimeInformation.RuntimeIdentifier, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (rid.StartsWith("win", StringComparison.OrdinalIgnoreCase) && OperatingSystem.IsWindows())
        {
            return 50;
        }

        if (rid.StartsWith("linux", StringComparison.OrdinalIgnoreCase) && OperatingSystem.IsLinux())
        {
            return 50;
        }

        if (rid.StartsWith("osx", StringComparison.OrdinalIgnoreCase) && OperatingSystem.IsMacOS())
        {
            return 50;
        }

        return 10;
    }

    /// <summary>
    /// 包安装目录，结构 Nugets/{id}/{version}
    /// </summary>
    private static string GetPackageDir(string id, string version)
    {
        return Path.Combine(s_root, id, version);
    }

    /// <summary>
    /// 从 lib/{tfm}/x.dll 取 tfm
    /// </summary>
    private static string GetTfm(string entry)
    {
        var parts = entry.Split('/');
        return parts.Length >= 3 ? parts[1] : "";
    }

    /// <summary>
    /// 给 TFM 打分，用于挑最合适的目标框架
    /// </summary>
    private static int ScoreTfm(string tfm)
    {
        var name = tfm.ToLowerInvariant();

        if (name == "net10.0")
        {
            return 1000;
        }

        if (name.StartsWith("net", StringComparison.Ordinal)
            && Version.TryParse(name[3..], out var version))
        {
            return 900 + version.Major * 10 + version.Minor;
        }

        return name switch
        {
            "netstandard2.1" => 500,
            "netstandard2.0" => 400,
            "netcoreapp3.1" => 300,
            _ => name.StartsWith("netcoreapp", StringComparison.Ordinal) ? 200 : 100,
        };
    }

    /// <summary>
    /// NuGet 程序集共用一个加载上下文，保证多个组件拿到同一份类型
    /// </summary>
    private sealed class NugetLoadContext(Dictionary<string, string> assemblies)
        : AssemblyLoadContext("PlugBotSharp.Nuget")
    {
        protected override Assembly? Load(AssemblyName name)
        {
            if (name.Name is null || !assemblies.TryGetValue(name.Name, out var path))
            {
                return null;
            }

            return LoadFromAssemblyPath(path);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            foreach (var candidate in ComponentLoader.NativeCandidates(unmanagedDllName))
            {
                if (TryResolveNative(candidate, out var path))
                {
                    return LoadUnmanagedDllFromPath(path);
                }
            }

            return IntPtr.Zero;
        }
    }
}
