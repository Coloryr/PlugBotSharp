namespace PlugBotSharp.Core.Nuget;

/// <summary>
/// 目标框架的归一化和打分：nuspec 里写的是 .NETStandard2.0 这种，
/// 包里目录写的是 netstandard2.0，这里统一成小写目录名再打个分挑最合适的
/// </summary>
internal static class NugetTfm
{
    /// <summary>
    /// 给目标框架打分，越高越合适，net10.0 最高
    /// </summary>
    /// <param name="tfm">目标框架，nuspec 的 targetFramework 或者 lib 下的目录名</param>
    public static int Score(string tfm)
    {
        var name = Normalize(tfm);

        if (name.Length == 0)
        {
            return 100;
        }

        if (name == "netstandard")
        {
            return 400;
        }

        if (name == "netstandard2.1")
        {
            return 500;
        }

        if (name == "netstandard2.0")
        {
            return 400;
        }

        if (name.StartsWith("netstandard", StringComparison.Ordinal))
        {
            return 300;
        }

        if (name.StartsWith("netcoreapp", StringComparison.Ordinal))
        {
            return Version.TryParse(name["netcoreapp".Length..], out var core)
                ? 200 + core.Major * 10 + core.Minor
                : 200;
        }

        if (name.StartsWith("net", StringComparison.Ordinal) && Version.TryParse(name[3..], out var version))
        {
            return 900 + version.Major * 10 + version.Minor;
        }

        return 100;
    }

    /// <summary>
    /// 带不带平台后缀，lib 选目录时优先不限定平台的版本，
    /// 因为宿主自己是 net10.0，net10.0-windows 那种可能依赖桌面框架
    /// </summary>
    /// <param name="tfm">目标框架</param>
    public static bool IsPlatformSpecific(string tfm)
    {
        return (tfm ?? "").Trim().IndexOf('-') > 0;
    }

    /// <summary>
    /// 平台后缀的加分：不限定平台的排中间，当前系统的平台版本优先，
    /// 别的平台（android / ios / tizen 之类）排最后，免得把移动端的依赖装进来
    /// </summary>
    /// <param name="tfm">目标框架</param>
    public static int PlatformBonus(string tfm)
    {
        var name = (tfm ?? "").Trim().ToLowerInvariant();
        var dash = name.IndexOf('-');

        if (dash < 0)
        {
            return 5;
        }

        var platform = name[(dash + 1)..];

        if (platform.StartsWith("windows", StringComparison.Ordinal))
        {
            return OperatingSystem.IsWindows() ? 20 : -50;
        }

        if (platform.StartsWith("linux", StringComparison.Ordinal))
        {
            return OperatingSystem.IsLinux() ? 20 : -50;
        }

        if (platform.StartsWith("macos", StringComparison.Ordinal)
            || platform.StartsWith("maccatalyst", StringComparison.Ordinal)
            || platform.StartsWith("osx", StringComparison.Ordinal))
        {
            return OperatingSystem.IsMacOS() ? 20 : -50;
        }

        return -50;
    }

    /// <summary>
    /// 归一化成 lib 目录那种写法：.NETStandard2.0 / net8.0-windows7.0 -> netstandard2.0 / net8.0
    /// </summary>
    /// <param name="tfm">目标框架</param>
    public static string Normalize(string tfm)
    {
        var name = (tfm ?? "").Trim().ToLowerInvariant();

        if (name.StartsWith(".netstandard", StringComparison.Ordinal))
        {
            name = "netstandard" + name[".netstandard".Length..];
        }
        else if (name.StartsWith(".netcoreapp", StringComparison.Ordinal))
        {
            name = "netcoreapp" + name[".netcoreapp".Length..];
        }
        else if (name.StartsWith(".netframework", StringComparison.Ordinal))
        {
            name = "netframework" + name[".netframework".Length..];
        }

        // 平台后缀（net8.0-windows7.0）对选 lib 没影响
        var dash = name.IndexOf('-');

        return dash > 0 ? name[..dash] : name;
    }
}
