using System.IO.Compression;

namespace PlugBotSharp.Core;

/// <summary>
/// 证书信息
/// </summary>
/// <param name="Name">证书名，配置里就填这个名字</param>
/// <param name="File">pfx 完整路径</param>
/// <param name="Password">pfx 密码，来自清单或者同目录的密码文件</param>
public sealed record CertInfo(string Name, string File, string Password);

/// <summary>
/// 证书仓库：Certs 目录下的 pfx / zip 统一由宿主管理，组件只填名字
/// </summary>
public static class CertStore
{
    /// <summary>
    /// 证书清单文件名，放在 Certs 目录下，密码可以不写
    /// </summary>
    public const string ManifestName = "Certs.json";

    /// <summary>
    /// 自动识别的密码文件名，优先级从高到低
    /// </summary>
    private static readonly string[] PasswordFiles = ["password.txt", "pwd.txt", "密码.txt", "cert.txt"];

    private static readonly Lock s_lock = new();

    private static string s_path = "";

    private static List<CertInfo> s_items = [];

    /// <summary>
    /// 初始化证书目录
    /// </summary>
    /// <param name="path">Certs 目录</param>
    public static void Init(string path)
    {
        s_path = path;
        Directory.CreateDirectory(path);
        Reload();
    }

    /// <summary>
    /// 重新扫描证书目录：先解压 zip，再按清单和密码文件补全密码
    /// </summary>
    public static void Reload()
    {
        if (string.IsNullOrEmpty(s_path))
        {
            return;
        }

        var items = new List<CertInfo>();

        // 1. 解压丢进来的 zip，里面一般是 pfx + password.txt
        ExtractArchives();

        // 2. 清单里的优先，密码没写就找密码文件
        foreach (var item in ReadManifest())
        {
            var file = System.IO.Path.IsPathRooted(item.File) ? item.File : System.IO.Path.Combine(s_path, item.File);

            items.Add(new CertInfo(item.Name, file,
                string.IsNullOrEmpty(item.Password) ? FindPassword(file) : item.Password));
        }

        // 3. 扫顶层和一层子目录（zip 解压出来的），没登记的按文件名补上
        foreach (var folder in EnumerateFolders())
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.pfx"))
            {
                if (items.Any(x => string.Equals(x.File, file, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var name = System.IO.Path.GetFileNameWithoutExtension(file);

                if (items.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                items.Add(new CertInfo(name, file, FindPassword(file)));
            }
        }

        lock (s_lock)
        {
            s_items = items;
        }
    }

    /// <summary>
    /// 列出全部证书
    /// </summary>
    public static IReadOnlyList<CertInfo> List()
    {
        lock (s_lock)
        {
            return [.. s_items];
        }
    }

    /// <summary>
    /// 全部证书名，给配置里的下拉框用
    /// </summary>
    public static string[] Names()
    {
        lock (s_lock)
        {
            return [.. s_items.Select(x => x.Name)];
        }
    }

    /// <summary>
    /// 按名字找证书
    /// </summary>
    /// <param name="name">证书名</param>
    /// <param name="info">证书信息</param>
    public static bool TryFind(string name, out CertInfo? info)
    {
        lock (s_lock)
        {
            info = s_items.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        return info is not null;
    }

    /// <summary>
    /// 解析配置里的证书：先按名字找仓库，找不到再当本地路径
    /// </summary>
    /// <param name="nameOrPath">证书名或者 pfx 路径</param>
    /// <param name="password">配置里填的密码，仓库里找到时以仓库为准</param>
    /// <param name="file">pfx 完整路径</param>
    /// <param name="resolvedPassword">最终使用的密码</param>
    public static bool TryResolve(string? nameOrPath, string? password, out string file, out string? resolvedPassword)
    {
        file = "";
        resolvedPassword = password;

        if (string.IsNullOrWhiteSpace(nameOrPath))
        {
            return false;
        }

        var value = nameOrPath.Trim();

        if (TryFind(value, out var info) && info is not null)
        {
            if (!System.IO.File.Exists(info.File))
            {
                return false;
            }

            file = info.File;
            resolvedPassword = info.Password;

            return true;
        }

        if (System.IO.File.Exists(value))
        {
            file = value;
            resolvedPassword = string.IsNullOrEmpty(password) ? FindPassword(value) : password;

            return true;
        }

        return false;
    }

    /// <summary>
    /// 证书目录
    /// </summary>
    public static string Root => s_path;

    /// <summary>
    /// 解压 Certs 下的 zip，已经解压过的不动
    /// </summary>
    private static void ExtractArchives()
    {
        foreach (var zip in Directory.EnumerateFiles(s_path, "*.zip"))
        {
            var target = System.IO.Path.Combine(s_path, System.IO.Path.GetFileNameWithoutExtension(zip));

            if (Directory.Exists(target))
            {
                continue;
            }

            try
            {
                ZipFile.ExtractToDirectory(zip, target, true);
            }
            catch (Exception e)
            {
                BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.CertExtractFail, zip));
            }
        }
    }

    /// <summary>
    /// 顶层 + 一层子目录，子目录用来放解压出来的证书
    /// </summary>
    private static IEnumerable<string> EnumerateFolders()
    {
        yield return s_path;

        foreach (var folder in Directory.EnumerateDirectories(s_path))
        {
            if (!System.IO.Path.GetFileName(folder).StartsWith('.'))
            {
                yield return folder;
            }
        }
    }

    /// <summary>
    /// 找 pfx 对应的密码文件，先找同名 txt，再找 password.txt 这类
    /// </summary>
    /// <param name="pfx">pfx 路径</param>
    private static string FindPassword(string pfx)
    {
        var folder = System.IO.Path.GetDirectoryName(pfx)!;
        var name = System.IO.Path.GetFileNameWithoutExtension(pfx);

        foreach (var candidate in new[] { name + ".txt", name + ".password" })
        {
            var path = System.IO.Path.Combine(folder, candidate);

            if (System.IO.File.Exists(path))
            {
                return ReadPassword(path);
            }
        }

        foreach (var candidate in PasswordFiles)
        {
            var path = System.IO.Path.Combine(folder, candidate);

            if (System.IO.File.Exists(path))
            {
                return ReadPassword(path);
            }
        }

        return "";
    }

    /// <summary>
    /// 读密码文件，取第一行非空内容
    /// </summary>
    private static string ReadPassword(string path)
    {
        try
        {
            foreach (var line in System.IO.File.ReadAllLines(path))
            {
                var value = line.Trim();

                if (value.Length > 0)
                {
                    return value;
                }
            }
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.CertPasswordReadFail, path));
        }

        return "";
    }

    /// <summary>
    /// 读证书清单
    /// </summary>
    private static List<Configs.CertItemObj> ReadManifest()
    {
        var manifest = System.IO.Path.Combine(s_path, ManifestName);

        if (!System.IO.File.Exists(manifest))
        {
            return [];
        }

        try
        {
            var obj = Configs.JsonUtils.ToObj(System.IO.File.ReadAllText(manifest),
                Configs.SourceGenerationContext.Default.CertFileObj);

            return (obj?.Items ?? []).Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.File)).ToList();
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.CertManifestInvalid, manifest));
            return [];
        }
    }
}
