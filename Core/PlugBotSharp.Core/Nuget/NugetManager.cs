using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core.Nuget;

/// <summary>
/// NuGet 依赖下载、解压与加载。
/// 装一个包时顺带读它的 nuspec，把子依赖一起装上，native 库按 RID 收集起来
/// </summary>
internal static class NugetManager
{
    private const string PackageUrl = "https://api.nuget.org/v3-flatcontainer/{0}/{1}/{0}.{1}.nupkg";
    private const string VersionUrl = "https://api.nuget.org/v3-flatcontainer/{0}/index.json";

    /// <summary>
    /// 子依赖最多往下找几层，防止依赖链出问题时一直递归
    /// </summary>
    private const int MaxDepth = 8;

    private static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private static readonly Dictionary<string, string> s_assemblies = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> s_natives = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, List<string>> s_versions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly NugetLoadContext s_context = new(s_assemblies);
    private static readonly Lock s_lock = new();

    private static string s_root = "";

    /// <summary>
    /// 初始化，指定 Nugets 目录
    /// </summary>
    /// <param name="root">Nugets 目录</param>
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

        foreach (var package in Directory.EnumerateDirectories(s_root))
        {
            foreach (var version in Directory.EnumerateDirectories(package))
            {
                RegisterPackage(version);
            }
        }
    }

    /// <summary>
    /// 确保组件声明的 NuGet 依赖都可用，子依赖一起装
    /// </summary>
    /// <param name="meta">组件元数据</param>
    public static bool Ensure(MetaObj meta)
    {
        foreach (var dep in meta.Deps ?? [])
        {
            if (dep.Type != DepType.Nuget)
            {
                continue;
            }

            if (!EnsurePackage(dep.Id, dep.Version))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 安装一个包和它的全部子依赖，版本可以写范围
    /// </summary>
    /// <param name="id">包 id</param>
    /// <param name="spec">版本或者版本范围</param>
    public static bool EnsurePackage(string id, string spec)
    {
        return InstallTree(id, spec, new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0, true);
    }

    /// <summary>
    /// 安装指定版本的包，已安装时直接复用，不会去读子依赖
    /// </summary>
    /// <param name="id">包 id</param>
    /// <param name="version">具体版本</param>
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
                RegisterPackage(dir);
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

            using (var archive = ZipFile.OpenRead(nupkg))
            {
                if (!Extract(archive, dir, id, version))
                {
                    return false;
                }
            }

            File.WriteAllText(marker, id + " " + version);

            RegisterPackage(dir);

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
    /// <param name="name">程序集名</param>
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

    /// <summary>
    /// 递归安装一个包和它的子依赖
    /// </summary>
    /// <param name="id">包 id</param>
    /// <param name="spec">版本或者版本范围</param>
    /// <param name="visited">这次安装已经处理过的 包/版本</param>
    /// <param name="depth">当前层级</param>
    /// <param name="required">装不上是否算失败，组件直接声明的包算，子依赖只记日志</param>
    private static bool InstallTree(string id, string spec, HashSet<string> visited, int depth, bool required)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return true;
        }

        if (depth > MaxDepth)
        {
            BotManager.Logger?.Warn(BotLang.GetLang(BotLangKey.NugetDependencyFail, id, spec ?? "", "依赖层级过深"));

            return !required;
        }

        var version = ResolveVersion(id, spec);

        if (version is null)
        {
            BotManager.Logger?.Warn(BotLang.GetLang(
                BotLangKey.NugetDependencyFail, id, spec ?? "", BotLang.GetLang(BotLangKey.NugetVersionNotFound)));

            return !required;
        }

        if (!visited.Add(id + "/" + version))
        {
            return true;
        }

        if (!Install(id, version))
        {
            return !required;
        }

        foreach (var child in ReadDependencies(GetPackageDir(id, version)))
        {
            // 子依赖装不上不连累组件，海外的 native 包缺一个也还能跑
            InstallTree(child.Id, child.Range, visited, depth + 1, false);
        }

        return true;
    }

    /// <summary>
    /// 把版本或者版本范围解析成具体版本
    /// </summary>
    /// <param name="id">包 id</param>
    /// <param name="spec">版本或者版本范围</param>
    private static string? ResolveVersion(string id, string spec)
    {
        var text = (spec ?? "").Trim();

        // 写死版本号的最常见，直接拿去下载，省一次版本列表请求
        if (text.Length > 0 && NugetVersion.IsPlainVersion(text))
        {
            return text;
        }

        var versions = GetVersions(id);

        if (versions is null || versions.Count == 0)
        {
            return null;
        }

        if (text.Length == 0 || text == "*")
        {
            return versions[^1];
        }

        return NugetVersion.TryPick(versions, text, out var picked) ? picked : null;
    }

    /// <summary>
    /// 取一个包的全部已发布版本，结果缓存在内存里
    /// </summary>
    /// <param name="id">包 id</param>
    private static List<string> GetVersions(string id)
    {
        return s_versions.GetOrAdd(id.ToLowerInvariant(), LoadVersions);
    }

    /// <summary>
    /// 从 NuGet 拉一个包的版本列表
    /// </summary>
    /// <param name="id">包 id</param>
    private static List<string> LoadVersions(string id)
    {
        var url = string.Format(VersionUrl, id);

        try
        {
            using var response = s_http.GetAsync(url).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                BotManager.Logger?.Error(BotLang.GetLang(BotLangKey.NugetDownloadFail, id, "*", (int)response.StatusCode));

                return [];
            }

            using var stream = response.Content.ReadAsStream();
            using var document = JsonDocument.Parse(stream);

            if (!document.RootElement.TryGetProperty("versions", out var array))
            {
                return [];
            }

            var versions = array
                .EnumerateArray()
                .Select(x => x.GetString() ?? "")
                .Where(x => x.Length > 0)
                .ToList();

            versions.Sort(NugetVersion.Compare);

            return versions;
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.NugetDownloadFail, id, "*", e.Message));

            return [];
        }
    }

    /// <summary>
    /// 下载 nupkg 到临时文件
    /// </summary>
    /// <param name="id">包 id</param>
    /// <param name="version">版本</param>
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
    /// 解压 nupkg：nuspec、最合适的 lib、平台相关的托管程序集、native 库
    /// </summary>
    /// <param name="archive">nupkg</param>
    /// <param name="dir">安装目录</param>
    /// <param name="id">包 id</param>
    /// <param name="version">版本</param>
    internal static bool Extract(ZipArchive archive, string dir, string id, string version)
    {
        var nuspec = archive.Entries
            .FirstOrDefault(e => e.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

        if (nuspec is not null)
        {
            // 留着给子依赖解析和排查问题用
            nuspec.ExtractToFile(Path.Combine(dir, id + ".nuspec"), true);
        }

        var extracted = ExtractManaged(archive, dir);
        extracted |= ExtractRuntimeManaged(archive, dir);
        extracted |= ExtractNative(archive, dir);

        // 只有 nuspec 的元包（例如 Microsoft.NETCore.Platforms）没什么可加载的，也算装好
        if (!extracted && nuspec is null)
        {
            BotManager.Logger?.Error(BotLang.GetLang(BotLangKey.NugetNoLib, id, version));
            return false;
        }

        return true;
    }

    /// <summary>
    /// 解压 lib/{tfm} 里最合适的一组托管程序集
    /// </summary>
    /// <param name="archive">nupkg</param>
    /// <param name="dir">安装目录</param>
    private static bool ExtractManaged(ZipArchive archive, string dir)
    {
        var entries = archive.Entries
            .Where(e => e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)
                     && e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (entries.Count == 0)
        {
            return false;
        }

        // 平台相关的目标框架排在后面，宿主是 net10.0，优先拿不限平台的版本
        var group = entries
            .GroupBy(e => GetTfm(e.FullName))
            .OrderByDescending(g => NugetTfm.Score(g.Key))
            .ThenByDescending(g => NugetTfm.IsPlatformSpecific(g.Key) ? 0 : 1)
            .First();

        var libDir = Path.Combine(dir, "lib", group.Key);
        Directory.CreateDirectory(libDir);

        foreach (var entry in group)
        {
            entry.ExtractToFile(Path.Combine(libDir, entry.Name), true);
        }

        return true;
    }

    /// <summary>
    /// 解压 runtimes/{rid}/lib/{tfm} 里最合适的托管程序集，有些包把平台相关的实现放这儿
    /// </summary>
    /// <param name="archive">nupkg</param>
    /// <param name="dir">安装目录</param>
    private static bool ExtractRuntimeManaged(ZipArchive archive, string dir)
    {
        var entries = archive.Entries
            .Where(e => e.FullName.StartsWith("runtimes/", StringComparison.OrdinalIgnoreCase)
                     && e.FullName.Contains("/lib/", StringComparison.OrdinalIgnoreCase)
                     && e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (entries.Count == 0)
        {
            return false;
        }

        var group = entries
            .GroupBy(e => GetRid(e.FullName))
            .OrderByDescending(g => ScoreRid(g.Key))
            .First();

        foreach (var entry in group)
        {
            var target = Path.Combine(dir, entry.FullName.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, true);
        }

        return true;
    }

    /// <summary>
    /// 解压 runtimes/{rid}/native 下的 native 库，全部留着按 RID 打分挑
    /// </summary>
    /// <param name="archive">nupkg</param>
    /// <param name="dir">安装目录</param>
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
    /// 把一个包目录里的程序集和 native 库注册进索引
    /// </summary>
    /// <param name="dir">包目录</param>
    private static void RegisterPackage(string dir)
    {
        var lib = Path.Combine(dir, "lib");

        if (Directory.Exists(lib))
        {
            Register(lib);
        }

        var runtimes = Path.Combine(dir, "runtimes");

        if (!Directory.Exists(runtimes))
        {
            return;
        }

        foreach (var runtimeLib in Directory.EnumerateDirectories(runtimes, "lib", SearchOption.AllDirectories))
        {
            Register(runtimeLib);
        }

        RegisterNative(dir);
    }

    /// <summary>
    /// 把 lib 目录下的 dll 注册到程序集索引，同一个程序集装了多个版本时留新的
    /// </summary>
    /// <param name="libRoot">lib 目录</param>
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
                var name = Path.GetFileNameWithoutExtension(file);

                if (s_assemblies.TryGetValue(name, out var current)
                    && NugetVersion.Compare(GetPackageVersion(file), GetPackageVersion(current)) <= 0)
                {
                    continue;
                }

                s_assemblies[name] = file;
            }
        }
    }

    /// <summary>
    /// 把包里的 native 库注册到索引，当前 RID 优先
    /// </summary>
    /// <param name="packageDir">包目录</param>
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
                var parts = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // 只认 native 目录，runtimes/{rid}/lib 下是托管程序集
                if (Array.FindIndex(parts, p => p.Equals("native", StringComparison.OrdinalIgnoreCase)) < 0)
                {
                    continue;
                }

                var name = Path.GetFileName(file);

                if (!s_natives.TryGetValue(name, out var current) || ScoreRid(GetRid(file)) > ScoreRid(GetRid(current)))
                {
                    s_natives[name] = file;
                }
            }
        }
    }

    /// <summary>
    /// 从路径里取 Nugets/{id}/{version} 的 version
    /// </summary>
    /// <param name="path">文件路径</param>
    private static string GetPackageVersion(string path)
    {
        if (string.IsNullOrEmpty(s_root))
        {
            return "";
        }

        var parts = Path.GetRelativePath(s_root, path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return parts.Length >= 2 ? parts[1] : "";
    }

    /// <summary>
    /// 给 RID 打分，当前 RID 的优先级最高
    /// </summary>
    /// <param name="rid">运行时标识，例如 win-x64</param>
    private static int ScoreRid(string rid)
    {
        if (string.IsNullOrEmpty(rid))
        {
            return 0;
        }

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
    /// 从 runtimes/{rid}/... 里取 rid
    /// </summary>
    /// <param name="path">路径</param>
    private static string GetRid(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var index = Array.FindIndex(parts, p => p.Equals("runtimes", StringComparison.OrdinalIgnoreCase));

        return index >= 0 && index + 1 < parts.Length ? parts[index + 1] : "";
    }

    /// <summary>
    /// 包安装目录，结构 Nugets/{id}/{version}
    /// </summary>
    /// <param name="id">包 id</param>
    /// <param name="version">版本</param>
    private static string GetPackageDir(string id, string version)
    {
        return Path.Combine(s_root, id, version);
    }

    /// <summary>
    /// 读包目录里 nuspec 记下的子依赖
    /// </summary>
    /// <param name="dir">包目录</param>
    private static List<NugetDependency> ReadDependencies(string dir)
    {
        var file = Directory
            .EnumerateFiles(dir, "*.nuspec", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        if (file is null)
        {
            return [];
        }

        try
        {
            using var stream = File.OpenRead(file);

            return NugetNuspec.Read(stream);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// 从 lib/{tfm}/x.dll 取 tfm
    /// </summary>
    /// <param name="entry">压缩包里的路径</param>
    private static string GetTfm(string entry)
    {
        var parts = entry.Split('/');

        return parts.Length >= 3 ? parts[1] : "";
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
