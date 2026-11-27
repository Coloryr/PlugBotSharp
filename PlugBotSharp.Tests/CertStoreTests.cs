using PlugBotSharp.Api;

namespace PlugBotSharp.Tests;

/// <summary>
/// 宿主的证书仓库：Certs 目录 + 清单
/// </summary>
public class CertStoreTests
{
    [Fact]
    public void Manifest_and_unlisted_files_are_listed_once()
    {
        var folder = CreateFolder();

        try
        {
            File.WriteAllBytes(Path.Combine(folder, "a.pfx"), []);
            File.WriteAllBytes(Path.Combine(folder, "b.pfx"), []);
            File.WriteAllText(Path.Combine(folder, "Certs.json"), """
                { "Items": [ { "Name": "panel", "File": "a.pfx", "Password": "123456" } ] }
                """);

            CertStore.Init(folder);

            var names = CertStore.List().Select(x => x.Name).OrderBy(x => x).ToArray();

            // a.pfx 已经被清单里的 panel 用掉了，不会再按文件名列一次
            Assert.Equal(["b", "panel"], names);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Resolve_by_name_uses_the_manifest_password()
    {
        var folder = CreateFolder();

        try
        {
            var file = Path.Combine(folder, "panel.pfx");
            File.WriteAllBytes(file, []);
            File.WriteAllText(Path.Combine(folder, "Certs.json"), """
                { "Items": [ { "Name": "panel", "File": "panel.pfx", "Password": "123456" } ] }
                """);

            CertStore.Init(folder);

            Assert.True(CertStore.TryResolve("panel", "ignored", out var resolved, out var password));
            Assert.Equal(file, resolved);
            Assert.Equal("123456", password);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Resolve_falls_back_to_a_local_path()
    {
        var folder = CreateFolder();

        try
        {
            var file = Path.Combine(folder, "other.pfx");
            File.WriteAllBytes(file, []);

            CertStore.Init(folder);

            Assert.True(CertStore.TryResolve(file, "pwd", out var resolved, out var password));
            Assert.Equal(file, resolved);
            Assert.Equal("pwd", password);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Unknown_cert_is_rejected()
    {
        var folder = CreateFolder();

        try
        {
            CertStore.Init(folder);

            Assert.False(CertStore.TryResolve("missing", null, out _, out _));
            Assert.False(CertStore.TryResolve("", null, out _, out _));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Manifest_entry_with_missing_file_is_rejected()
    {
        var folder = CreateFolder();

        try
        {
            File.WriteAllText(Path.Combine(folder, "Certs.json"), """
                { "Items": [ { "Name": "panel", "File": "gone.pfx", "Password": "" } ] }
                """);

            CertStore.Init(folder);

            Assert.False(CertStore.TryResolve("panel", null, out _, out _));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// 建一个临时证书目录
    /// </summary>
    private static string CreateFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pbs-certs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        return folder;
    }
}