using System.IO.Compression;
using System.Text;
using PlugBotSharp.Core.Components;

namespace PlugBotSharp.Tests;

/// <summary>
/// 组件安装包的安装与删除
/// </summary>
public class ComponentInstallerTests : ComponentTestBase
{
    /// <summary>
    /// 正常安装：清单在根目录
    /// </summary>
    [Fact]
    public void InstallZip_RootManifest()
    {
        using var zip = CreateZip(new Dictionary<string, string>
        {
            ["Meta.json"] = Manifest("demo"),
            ["Demo.dll"] = "dll"
        });

        Assert.True(ComponentInstaller.TryInstallZip(zip, out var meta, out var error), error);
        Assert.Equal("demo", meta!.Id);

        var folder = Path.Combine(Root, "Components", "demo");
        Assert.True(File.Exists(Path.Combine(folder, "Meta.json")));
        Assert.True(File.Exists(Path.Combine(folder, "Demo.dll")));

        // 安装临时目录不能留下
        Assert.DoesNotContain(Directory.GetDirectories(Path.Combine(Root, "Components")), x => Path.GetFileName(x).StartsWith('.'));
    }

    /// <summary>
    /// 清单在压缩包的子目录里，安装后应该铺平到组件目录
    /// </summary>
    [Fact]
    public void InstallZip_NestedManifest()
    {
        using var zip = CreateZip(new Dictionary<string, string>
        {
            ["pkg/Meta.json"] = Manifest("nested"),
            ["pkg/Demo.dll"] = "dll"
        });

        Assert.True(ComponentInstaller.TryInstallZip(zip, out var meta, out var error), error);
        Assert.Equal("nested", meta!.Id);

        var folder = Path.Combine(Root, "Components", "nested");
        Assert.True(File.Exists(Path.Combine(folder, "Meta.json")));
        Assert.True(File.Exists(Path.Combine(folder, "Demo.dll")));
        Assert.False(Directory.Exists(Path.Combine(folder, "pkg")));
    }

    /// <summary>
    /// 清单声明了 dll 但包里没有，安装失败且不留目录
    /// </summary>
    [Fact]
    public void InstallZip_MissingDll()
    {
        using var zip = CreateZip(new Dictionary<string, string>
        {
            ["Meta.json"] = Manifest("broken")
        });

        Assert.False(ComponentInstaller.TryInstallZip(zip, out _, out var error));
        Assert.True(error.Length > 0);
        Assert.False(Directory.Exists(Path.Combine(Root, "Components", "broken")));
    }

    /// <summary>
    /// 压缩包里没有清单
    /// </summary>
    [Fact]
    public void InstallZip_NoManifest()
    {
        using var zip = CreateZip(new Dictionary<string, string>
        {
            ["Demo.dll"] = "dll"
        });

        Assert.False(ComponentInstaller.TryInstallZip(zip, out _, out var error));
        Assert.True(error.Length > 0);
    }

    /// <summary>
    /// 越界路径不能写到组件目录外面
    /// </summary>
    [Fact]
    public void InstallZip_PathTraversal()
    {
        using var zip = CreateZip(new Dictionary<string, string>
        {
            ["Meta.json"] = Manifest("evil"),
            ["Demo.dll"] = "dll",
            ["../evil.txt"] = "escape"
        });

        Assert.False(ComponentInstaller.TryInstallZip(zip, out _, out _));
        Assert.False(File.Exists(Path.Combine(Root, "evil.txt")));
        Assert.False(Directory.Exists(Path.Combine(Root, "Components", "evil")));
    }

    /// <summary>
    /// id 同时是目录名，非法 id 直接拒绝
    /// </summary>
    [Fact]
    public void InstallZip_InvalidId()
    {
        using var zip = CreateZip(new Dictionary<string, string>
        {
            ["Meta.json"] = Manifest("../escape"),
            ["Demo.dll"] = "dll"
        });

        Assert.False(ComponentInstaller.TryInstallZip(zip, out _, out var error));
        Assert.True(error.Length > 0);
    }

    /// <summary>
    /// 从目录安装
    /// </summary>
    [Fact]
    public void InstallFolder_CopiesFiles()
    {
        var source = Path.Combine(Root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Meta.json"), Manifest("qq", "Proxy"));
        File.WriteAllText(Path.Combine(source, "Demo.dll"), "dll");

        Assert.True(ComponentInstaller.TryInstallFolder(source, out var meta, out var error), error);
        Assert.Equal("qq", meta!.Id);
        Assert.True(File.Exists(Path.Combine(Root, "Components", "qq", "Demo.dll")));
    }

    /// <summary>
    /// 只认 Meta.json，旧名字的清单不再安装
    /// </summary>
    [Fact]
    public void Install_RequiresMetaJsonName()
    {
        var source = Path.Combine(Root, "legacy");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Proxy.json"), Manifest("legacy", "Proxy"));
        File.WriteAllText(Path.Combine(source, "Demo.dll"), "dll");

        Assert.False(ComponentInstaller.TryInstallFolder(source, out _, out var error));
        Assert.True(error.Length > 0);
        Assert.False(Directory.Exists(Path.Combine(Root, "Components", "legacy")));

        using var zip = CreateZip(new Dictionary<string, string>
        {
            ["Plugin.json"] = Manifest("legacy2"),
            ["Demo.dll"] = "dll"
        });

        Assert.False(ComponentInstaller.TryInstallZip(zip, out _, out _));
    }

    /// <summary>
    /// 删除组件目录
    /// </summary>
    [Fact]
    public void Delete_RemovesFolder()
    {
        using var zip = CreateZip(new Dictionary<string, string>
        {
            ["Meta.json"] = Manifest("gone"),
            ["Demo.dll"] = "dll"
        });

        Assert.True(ComponentInstaller.TryInstallZip(zip, out _, out var error), error);
        Assert.True(Directory.Exists(Path.Combine(Root, "Components", "gone")));

        Assert.True(ComponentInstaller.TryDelete("gone", out error), error);
        Assert.False(Directory.Exists(Path.Combine(Root, "Components", "gone")));
    }

    /// <summary>
    /// 删除不存在的组件返回失败
    /// </summary>
    [Fact]
    public void Delete_MissingComponent()
    {
        Assert.False(ComponentInstaller.TryDelete("none", out var error));
        Assert.True(error.Length > 0);
    }

    /// <summary>
    /// 组件清单
    /// </summary>
    private static string Manifest(string id, string type = "Plugin")
    {
        return $$"""
        {
          "Name": "{{id}}",
          "Id": "{{id}}",
          "Version": "1.0.0",
          "Description": "test",
          "Type": "{{type}}",
          "Dlls": ["Demo.dll"],
          "Deps": []
        }
        """;
    }

    /// <summary>
    /// 内存里的 zip
    /// </summary>
    private static MemoryStream CreateZip(Dictionary<string, string> files)
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            foreach (var pair in files)
            {
                var entry = archive.CreateEntry(pair.Key, CompressionLevel.Fastest);

                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(pair.Value);
            }
        }

        stream.Position = 0;
        return stream;
    }
}
