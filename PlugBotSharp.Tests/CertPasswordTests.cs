using System.IO.Compression;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;

namespace PlugBotSharp.Tests;

/// <summary>
/// 证书密码文件识别和压缩包解压
/// </summary>
public class CertPasswordTests
{
    [Fact]
    public void Password_is_read_from_the_same_named_txt()
    {
        var folder = CreateFolder();

        try
        {
            var pfx = Path.Combine(folder, "panel.pfx");
            File.WriteAllBytes(pfx, []);
            File.WriteAllText(Path.Combine(folder, "panel.txt"), "123456\n");

            CertStore.Init(folder);

            Assert.True(CertStore.TryResolve("panel", null, out var file, out var password));
            Assert.Equal(pfx, file);
            Assert.Equal("123456", password);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Password_falls_back_to_password_txt()
    {
        var folder = CreateFolder();

        try
        {
            File.WriteAllBytes(Path.Combine(folder, "a.pfx"), []);
            File.WriteAllText(Path.Combine(folder, "password.txt"), "pwd");

            CertStore.Init(folder);

            Assert.True(CertStore.TryFind("a", out var info));
            Assert.Equal("pwd", info!.Password);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Zip_archive_is_extracted_and_picked_up()
    {
        var folder = CreateFolder();
        var inner = Path.Combine(folder, "src");
        Directory.CreateDirectory(inner);

        try
        {
            File.WriteAllBytes(Path.Combine(inner, "panel.pfx"), []);
            File.WriteAllText(Path.Combine(inner, "password.txt"), "123456");
            ZipFile.CreateFromDirectory(inner, Path.Combine(folder, "cert.zip"));
            Directory.Delete(inner, true);

            CertStore.Init(folder);

            Assert.True(CertStore.TryResolve("panel", null, out var file, out var password));
            Assert.EndsWith("panel.pfx", file);
            Assert.Equal("123456", password);
            Assert.True(Directory.Exists(Path.Combine(folder, "cert")));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Manifest_password_wins_over_the_txt()
    {
        var folder = CreateFolder();

        try
        {
            File.WriteAllBytes(Path.Combine(folder, "panel.pfx"), []);
            File.WriteAllText(Path.Combine(folder, "panel.txt"), "fromtxt");
            File.WriteAllText(Path.Combine(folder, "Certs.json"), """
                { "Items": [ { "Name": "panel", "File": "panel.pfx", "Password": "fromjson" } ] }
                """);

            CertStore.Init(folder);

            Assert.True(CertStore.TryResolve("panel", null, out _, out var password));
            Assert.Equal("fromjson", password);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Manifest_without_password_uses_the_txt()
    {
        var folder = CreateFolder();

        try
        {
            File.WriteAllBytes(Path.Combine(folder, "panel.pfx"), []);
            File.WriteAllText(Path.Combine(folder, "password.txt"), "fromtxt");
            File.WriteAllText(Path.Combine(folder, "Certs.json"), """
                { "Items": [ { "Name": "panel", "File": "panel.pfx" } ] }
                """);

            CertStore.Init(folder);

            Assert.True(CertStore.TryResolve("panel", null, out _, out var password));
            Assert.Equal("fromtxt", password);
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
        var folder = Path.Combine(Path.GetTempPath(), "pbs-certs-pwd", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        return folder;
    }
}
