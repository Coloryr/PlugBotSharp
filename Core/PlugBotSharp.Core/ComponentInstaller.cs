using System.IO.Compression;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core;

/// <summary>
/// 组件安装与删除，安装包为包含 Meta.json 和 dll 的 zip
/// </summary>
internal static class ComponentInstaller
{
    /// <summary>
    /// 组件清单文件名
    /// </summary>
    private const string ManifestName = "Meta.json";

    /// <summary>
    /// 从 zip 流安装组件，安装完成后不会自动加载
    /// </summary>
    /// <param name="stream">zip 内容</param>
    /// <param name="meta">安装后的组件元数据</param>
    /// <param name="error">失败原因</param>
    /// <returns>是否安装成功</returns>
    public static bool TryInstallZip(Stream stream, out MetaObj? meta, out string error)
    {
        meta = null;
        error = "";

        var zipFile = Path.Combine(Path.GetTempPath(), "pbs-" + Guid.NewGuid().ToString("N") + ".zip");

        try
        {
            using (var file = File.Create(zipFile))
            {
                stream.CopyTo(file);
            }

            using var archive = ZipFile.OpenRead(zipFile);

            var entry = FindManifest(archive);

            if (entry is null)
            {
                error = BotLang.GetLang(BotLangKey.ComponentInstallNoManifest);
                return false;
            }

            meta = ReadManifest(entry);

            if (meta is null)
            {
                error = BotLang.GetLang(BotLangKey.ComponentManifestInvalid, Path.GetFileName(entry.FullName), entry.FullName);
                return false;
            }

            if (!IsValidId(meta.Id))
            {
                error = BotLang.GetLang(BotLangKey.ComponentIdInvalid, meta.Id ?? "");
                return false;
            }

            return Install(meta, staging =>
            {
                var prefix = GetPrefix(entry.FullName);

                foreach (var item in archive.Entries)
                {
                    if (item.FullName.EndsWith('/'))
                    {
                        continue;
                    }

                    if (!item.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var relative = item.FullName[prefix.Length..].Replace('\\', '/');

                    if (relative.Length == 0)
                    {
                        continue;
                    }

                    ExtractEntry(item, staging, relative);
                }
            }, out error);
        }
        catch (Exception e)
        {
            error = e.Message;
            BotManager.Logger.Error(e, BotLang.GetLang(BotLangKey.ComponentInstallFail, "-", e.Message));
            return false;
        }
        finally
        {
            TryDeleteFile(zipFile);
        }
    }

    /// <summary>
    /// 从本地目录安装组件，目录内需要有 Meta.json
    /// </summary>
    /// <param name="source">源目录</param>
    /// <param name="meta">安装后的组件元数据</param>
    /// <param name="error">失败原因</param>
    /// <returns>是否安装成功</returns>
    public static bool TryInstallFolder(string source, out MetaObj? meta, out string error)
    {
        meta = null;
        error = "";

        if (!Directory.Exists(source))
        {
            error = BotLang.GetLang(BotLangKey.ComponentDirMissing, source);
            return false;
        }

        var manifest = FindManifest(source);

        if (manifest is null)
        {
            error = BotLang.GetLang(BotLangKey.ComponentInstallNoManifest);
            return false;
        }

        meta = ComponentLoader.TryReadMeta(source);

        if (meta is null)
        {
            error = BotLang.GetLang(BotLangKey.ComponentManifestInvalid, Path.GetFileName(manifest), manifest);
            return false;
        }

        if (!IsValidId(meta.Id))
        {
            error = BotLang.GetLang(BotLangKey.ComponentIdInvalid, meta.Id ?? "");
            return false;
        }

        return Install(meta, staging => CopyFolder(source, staging), out error);
    }

    /// <summary>
    /// 删除组件目录，删除前需要保证组件已经卸载
    /// </summary>
    /// <param name="id">组件 id</param>
    /// <param name="error">失败原因</param>
    /// <returns>是否删除成功</returns>
    public static bool TryDelete(string id, out string error)
    {
        error = "";

        if (!IsValidId(id))
        {
            error = BotLang.GetLang(BotLangKey.ComponentIdInvalid, id ?? "");
            return false;
        }

        if (ComponentManager.IsLoaded(id))
        {
            error = BotLang.GetLang(BotLangKey.ComponentInstallInUse, id);
            return false;
        }

        var folder = Path.Combine(BotManager.ComponentsPath, id);

        if (!Directory.Exists(folder))
        {
            error = BotLang.GetLang(BotLangKey.ComponentNotFound, id);
            return false;
        }

        try
        {
            Directory.Delete(folder, true);
        }
        catch (Exception e)
        {
            error = BotLang.GetLang(BotLangKey.ComponentDeleteFail, id, e.Message);
            BotManager.Logger.Error(e, error);
            return false;
        }

        StateManager.RemoveComponentState(id);
        BotManager.Logger.Info(BotLang.GetLang(BotLangKey.ComponentDeleteSuccess, id));

        return true;
    }

    /// <summary>
    /// 把组件内容放到 Components/{id}，先写到临时目录再整体替换
    /// </summary>
    /// <param name="meta">组件元数据</param>
    /// <param name="write">把内容写进临时目录</param>
    /// <param name="error">失败原因</param>
    private static bool Install(MetaObj meta, Action<string> write, out string error)
    {
        error = "";

        var id = meta.Id;
        var target = Path.Combine(BotManager.ComponentsPath, id);
        var staging = Path.Combine(BotManager.ComponentsPath, ".staging-" + Guid.NewGuid().ToString("N"));

        if (ComponentManager.IsLoaded(id))
        {
            error = BotLang.GetLang(BotLangKey.ComponentInstallInUse, id);
            return false;
        }

        try
        {
            Directory.CreateDirectory(staging);
            write(staging);

            // 清单声明的 dll 必须都在
            foreach (var dll in meta.Dlls ?? [])
            {
                var path = Path.Combine(staging, dll);

                if (!File.Exists(path))
                {
                    error = BotLang.GetLang(BotLangKey.ComponentDllMissing, path);
                    return false;
                }
            }

            if (Directory.Exists(target))
            {
                Directory.Delete(target, true);
            }

            Directory.Move(staging, target);
        }
        catch (Exception e)
        {
            error = BotLang.GetLang(BotLangKey.ComponentInstallFail, id, e.Message);
            BotManager.Logger.Error(e, error);
            return false;
        }
        finally
        {
            TryDeleteFolder(staging);
        }

        BotManager.Logger.Info(BotLang.GetLang(BotLangKey.ComponentInstallSuccess, id));

        return true;
    }

    /// <summary>
    /// 找到压缩包里的清单，取层级最浅的一个
    /// </summary>
    private static ZipArchiveEntry? FindManifest(ZipArchive archive)
    {
        ZipArchiveEntry? result = null;
        var depth = int.MaxValue;

        foreach (var entry in archive.Entries)
        {
            if (!string.Equals(Path.GetFileName(entry.FullName), ManifestName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var current = entry.FullName.Count(c => c == '/');

            if (current < depth)
            {
                depth = current;
                result = entry;
            }
        }

        return result;
    }

    /// <summary>
    /// 找到目录里的清单文件
    /// </summary>
    private static string? FindManifest(string folder)
    {
        var path = Path.Combine(folder, ManifestName);

        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// 读取压缩包里的清单
    /// </summary>
    private static MetaObj? ReadManifest(ZipArchiveEntry entry)
    {
        try
        {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream);

            return JsonUtils.ToObj(reader.ReadToEnd(), SourceGenerationContext.Default.MetaObj);
        }
        catch (Exception e)
        {
            BotManager.Logger.Error(e, BotLang.GetLang(BotLangKey.ComponentManifestInvalid, entry.FullName, entry.FullName));
            return null;
        }
    }

    /// <summary>
    /// 清单所在的目录前缀，例如 pkg/Meta.json 返回 pkg/
    /// </summary>
    private static string GetPrefix(string fullName)
    {
        var index = fullName.LastIndexOf('/');

        return index < 0 ? "" : fullName[..(index + 1)];
    }

    /// <summary>
    /// 解压单个文件，阻止路径越界
    /// </summary>
    private static void ExtractEntry(ZipArchiveEntry entry, string target, string relative)
    {
        var path = Path.GetFullPath(Path.Combine(target, relative));

        if (!IsInside(target, path))
        {
            throw new InvalidOperationException(BotLang.GetLang(BotLangKey.ComponentPathInvalid, entry.FullName));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var input = entry.Open();
        using var output = File.Create(path);
        input.CopyTo(output);
    }

    /// <summary>
    /// 复制目录到临时目录
    /// </summary>
    private static void CopyFolder(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var path = Path.GetFullPath(Path.Combine(target, relative));

            if (!IsInside(target, path))
            {
                throw new InvalidOperationException(BotLang.GetLang(BotLangKey.ComponentPathInvalid, file));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(file, path, true);
        }
    }

    /// <summary>
    /// 判断路径是否在目录内
    /// </summary>
    private static bool IsInside(string folder, string path)
    {
        var root = Path.GetFullPath(folder);

        if (!root.EndsWith(Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 组件 id 同时用作目录名，需要校验
    /// </summary>
    private static bool IsValidId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (id is "." or "..")
        {
            return false;
        }

        return id.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
            && !id.Contains('/')
            && !id.Contains('\\');
    }

    /// <summary>
    /// 删除目录，忽略异常
    /// </summary>
    private static void TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
        catch
        {
            // 临时目录清理失败不影响安装结果
        }
    }

    /// <summary>
    /// 删除文件，忽略异常
    /// </summary>
    private static void TryDeleteFile(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
        catch
        {
            // 临时文件清理失败不影响安装结果
        }
    }
}
