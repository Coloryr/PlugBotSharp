using System.Globalization;

namespace PlugBotSharp.Core.Nuget;

/// <summary>
/// 版本号比较与范围判断
/// </summary>
internal static class VersionUtil
{
    /// <summary>
    /// 判断版本是否满足范围，范围支持 >=、>、&lt;=、&lt;、=、== 以及裸版本号（最低版本）
    /// </summary>
    public static bool Satisfies(string? version, string? range)
    {
        if (string.IsNullOrWhiteSpace(range))
        {
            return true;
        }

        if (!TryParse(version, out var value))
        {
            return false;
        }

        foreach (var part in range.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!SatisfiesSingle(value, part))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 判断单个版本条件
    /// </summary>
    private static bool SatisfiesSingle(Version value, string part)
    {
        if (part.StartsWith(">=", StringComparison.Ordinal))
        {
            return TryParse(part[2..], out var min) && value >= min;
        }

        if (part.StartsWith("<=", StringComparison.Ordinal))
        {
            return TryParse(part[2..], out var max) && value <= max;
        }

        if (part.StartsWith(">", StringComparison.Ordinal))
        {
            return TryParse(part[1..], out var min) && value > min;
        }

        if (part.StartsWith("<", StringComparison.Ordinal))
        {
            return TryParse(part[1..], out var max) && value < max;
        }

        if (part.StartsWith("==", StringComparison.Ordinal))
        {
            return TryParse(part[2..], out var exact) && value == exact;
        }

        if (part.StartsWith("=", StringComparison.Ordinal))
        {
            return TryParse(part[1..], out var exact) && value == exact;
        }

        // 裸版本号按 NuGet 的语义当作最低版本
        return TryParse(part, out var lower) && value >= lower;
    }

    /// <summary>
    /// 解析版本号，统一补齐到 4 段，忽略预发布后缀
    /// </summary>
    public static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0, 0, 0);

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var core = text.Trim();

        var dash = core.IndexOfAny(['-', '+']);

        if (dash >= 0)
        {
            core = core[..dash];
        }

        var parts = core.Split('.');

        if (parts.Length is 0 or > 4)
        {
            return false;
        }

        var numbers = new int[4];

        for (var i = 0; i < 4; i++)
        {
            if (i < parts.Length)
            {
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                {
                    return false;
                }
            }
            else
            {
                numbers[i] = 0;
            }
        }

        version = new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
        return true;
    }
}
