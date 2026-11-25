using System.IO.Compression;
using System.Text;
using PlugBotSharp.Core.Nuget;

namespace PlugBotSharp.Tests;

/// <summary>
/// NuGet 依赖：版本范围、目标框架选择、子依赖解析、包解压
/// </summary>
public class NugetTests : ComponentTestBase
{
    [Fact]
    public void Version_compare_orders_prerelease_below_release()
    {
        Assert.True(NugetVersion.Compare("1.2.3", "1.2.10") < 0);
        Assert.True(NugetVersion.Compare("2.0.0", "1.9.9") > 0);
        Assert.Equal(0, NugetVersion.Compare("1.0", "1.0.0"));
        Assert.True(NugetVersion.Compare("1.0.0", "1.0.0-beta") > 0);
    }

    [Fact]
    public void Range_picks_the_lowest_matching_version()
    {
        var versions = new[] { "1.0.0", "1.5.0", "2.0.0", "3.0.0" };

        Assert.True(NugetVersion.TryPick(versions, "[1.0.0,2.0.0)", out var picked));
        Assert.Equal("1.0.0", picked);

        Assert.True(NugetVersion.TryPick(versions, "1.5.0", out picked));
        Assert.Equal("1.5.0", picked);
    }

    [Fact]
    public void Range_supports_exact_and_upper_bounds()
    {
        var versions = new[] { "1.0.0", "1.5.0", "2.0.0" };

        Assert.True(NugetVersion.TryPick(versions, "[1.5.0]", out var picked));
        Assert.Equal("1.5.0", picked);

        Assert.True(NugetVersion.TryPick(versions, "(,1.5.0]", out picked));
        Assert.Equal("1.0.0", picked);

        Assert.False(NugetVersion.TryPick(versions, "[3.0.0]", out _));
    }

    [Fact]
    public void Prerelease_is_skipped_unless_the_range_asks_for_it()
    {
        var versions = new[] { "1.0.0-beta", "1.0.0" };

        Assert.True(NugetVersion.TryPick(versions, "1.0.0", out var picked));
        Assert.Equal("1.0.0", picked);

        Assert.True(NugetVersion.TryPick(["1.0.0-beta"], "[1.0.0-beta,)", out picked));
        Assert.Equal("1.0.0-beta", picked);
    }

    [Fact]
    public void Plain_version_is_detected()
    {
        Assert.True(NugetVersion.IsPlainVersion("4.157.0"));
        Assert.True(NugetVersion.IsPlainVersion("1.0.0-beta.1"));
        Assert.False(NugetVersion.IsPlainVersion("[1.0.0,2.0.0)"));
        Assert.False(NugetVersion.IsPlainVersion(""));
    }

    [Fact]
    public void Tfm_is_normalized_and_scored()
    {
        Assert.Equal("netstandard2.0", NugetTfm.Normalize(".NETStandard2.0"));
        Assert.Equal("net8.0", NugetTfm.Normalize("net8.0-windows7.0"));

        Assert.True(NugetTfm.Score("net10.0") > NugetTfm.Score("netstandard2.0"));
        Assert.True(NugetTfm.Score(".NETStandard2.0") > NugetTfm.Score(".NETFramework4.7.2"));
    }

    [Fact]
    public void Nuspec_uses_the_best_target_framework_group()
    {
        const string xml = """
            <package>
              <metadata>
                <id>Demo</id>
                <dependencies>
                  <group targetFramework=".NETFramework4.7.2">
                    <dependency id="Old.Dep" version="1.0.0" />
                  </group>
                  <group targetFramework=".NETStandard2.0">
                    <dependency id="New.Dep" version="[2.0.0,)" />
                  </group>
                </dependencies>
              </metadata>
            </package>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var deps = NugetNuspec.Read(stream);

        var dep = Assert.Single(deps);
        Assert.Equal("New.Dep", dep.Id);
        Assert.Equal("[2.0.0,)", dep.Range);
    }

    [Fact]
    public void Nuspec_skips_a_mobile_only_group()
    {
        const string xml = """
            <package>
              <metadata>
                <dependencies>
                  <group targetFramework="net10.0-android36.0">
                    <dependency id="Android.Dep" version="1.0.0" />
                  </group>
                  <group targetFramework="net10.0">
                    <dependency id="Desktop.Dep" version="1.0.0" />
                  </group>
                </dependencies>
              </metadata>
            </package>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var deps = NugetNuspec.Read(stream);

        Assert.Equal("Desktop.Dep", Assert.Single(deps).Id);
    }

    [Fact]
    public void Extract_keeps_managed_runtime_and_native_files()
    {
        var dir = Path.Combine(Root, "pkgs", "Demo", "1.0.0");
        Directory.CreateDirectory(dir);

        using var stream = new MemoryStream();

        using (var write = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            Add(write, "Demo.nuspec", "<package><metadata><id>Demo</id></metadata></package>");
            Add(write, "lib/netstandard2.0/Demo.dll", "old");
            Add(write, "lib/net10.0/Demo.dll", "new");
            Add(write, "runtimes/win-x64/lib/net10.0/Demo.Runtime.dll", "runtime");
            Add(write, "runtimes/win-x64/native/libDemoNative.dll", "native");
            Add(write, "runtimes/linux-x64/native/libDemoNative.so", "native");
        }

        stream.Position = 0;

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            Assert.True(NugetManager.Extract(archive, dir, "Demo", "1.0.0"));
        }

        // 只解最合适的 TFM
        Assert.True(File.Exists(Path.Combine(dir, "lib", "net10.0", "Demo.dll")));
        Assert.False(File.Exists(Path.Combine(dir, "lib", "netstandard2.0", "Demo.dll")));

        // 平台相关的托管程序集和 native 库都留着
        Assert.True(File.Exists(Path.Combine(dir, "runtimes", "win-x64", "lib", "net10.0", "Demo.Runtime.dll")));
        Assert.True(File.Exists(Path.Combine(dir, "runtimes", "win-x64", "native", "libDemoNative.dll")));
        Assert.True(File.Exists(Path.Combine(dir, "Demo.nuspec")));
    }

    [Fact]
    public void Extract_prefers_the_plain_target_framework()
    {
        var dir = Path.Combine(Root, "pkgs", "Plain", "1.0.0");
        Directory.CreateDirectory(dir);

        using var stream = new MemoryStream();

        using (var write = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            Add(write, "lib/net10.0-windows10.0.19041/Plain.dll", "desktop");
            Add(write, "lib/net10.0/Plain.dll", "plain");
        }

        stream.Position = 0;

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            Assert.True(NugetManager.Extract(archive, dir, "Plain", "1.0.0"));
        }

        Assert.True(File.Exists(Path.Combine(dir, "lib", "net10.0", "Plain.dll")));
        Assert.False(Directory.Exists(Path.Combine(dir, "lib", "net10.0-windows10.0.19041")));
    }

    [Fact]
    public void Meta_package_with_only_a_nuspec_is_accepted()
    {
        var dir = Path.Combine(Root, "pkgs", "Meta", "1.0.0");
        Directory.CreateDirectory(dir);

        using var stream = new MemoryStream();

        using (var write = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            Add(write, "Meta.nuspec", "<package><metadata><id>Meta</id></metadata></package>");
        }

        stream.Position = 0;

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            Assert.True(NugetManager.Extract(archive, dir, "Meta", "1.0.0"));
        }

        Assert.False(Directory.Exists(Path.Combine(dir, "lib")));
    }

    [Fact]
    public void Scan_registers_native_libraries()
    {
        var nugets = Path.Combine(Root, "Nugets");
        var native = Path.Combine(nugets, "Demo", "1.0.0", "runtimes", "win-x64", "native");

        Directory.CreateDirectory(native);
        File.WriteAllText(Path.Combine(native, "libNugetScanTest.dll"), "native");

        NugetManager.Init(nugets);
        NugetManager.Scan();

        Assert.True(NugetManager.TryResolveNative("libNugetScanTest.dll", out var path));
        Assert.EndsWith("libNugetScanTest.dll", path);
    }

    /// <summary>
    /// 往压缩包里加一个文件
    /// </summary>
    /// <param name="archive">压缩包</param>
    /// <param name="name">路径</param>
    /// <param name="text">内容</param>
    private static void Add(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);

        writer.Write(text);
    }
}