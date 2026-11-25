namespace PlugBotSharp.Core.Nuget;

/// <summary>
/// NuGet 版本号比较和版本范围匹配。
/// 版本按 数字部分 + 可选预发布后缀 处理，范围支持 [1.0.0] / 1.0.0 /
/// [1.0.0,2.0.0) / (,2.0.0] / [1.0.0,) 这些写法
/// </summary>
internal static class NugetVersion
{
    /// <summary>
    /// 比较两个版本号，返回 -1 / 0 / 1
    /// </summary>
    /// <param name="left">左边的版本</param>
    /// <param name="right">右边的版本</param>
    public static int Compare(string left, string right)
    {
        var a = Split(left);
        var b = Split(right);

        var result = CompareNumbers(a.Numbers, b.Numbers);

        if (result != 0)
        {
            return result;
        }

        // 正式版比预发布版新，两个都带预发布就按字母比
        if (a.Pre.Length == 0 && b.Pre.Length == 0)
        {
            return 0;
        }

        if (a.Pre.Length == 0)
        {
            return 1;
        }

        if (b.Pre.Length == 0)
        {
            return -1;
        }

        return string.Compare(a.Pre, b.Pre, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 从可用版本里挑一个满足版本范围的，按 NuGet 的习惯取最低的
    /// </summary>
    /// <param name="versions">可用版本，顺序无所谓</param>
    /// <param name="spec">版本或者版本范围</param>
    /// <param name="picked">挑中的版本，没挑到是空字符串</param>
    /// <returns>是否挑到了版本</returns>
    public static bool TryPick(IEnumerable<string> versions, string spec, out string picked)
    {
        picked = "";

        if (!TryParseRange(spec, out var range))
        {
            return false;
        }

        var matches = versions
            .Where(x => !string.IsNullOrWhiteSpace(x) && InRange(x, range))
            .ToList();

        if (matches.Count == 0)
        {
            return false;
        }

        matches.Sort(Compare);

        picked = matches[0];
        return true;
    }

    /// <summary>
    /// 是不是一个纯版本号（没有范围符号），这种可以直接拿去下载，不用先查版本列表
    /// </summary>
    /// <param name="spec">版本或者版本范围</param>
    public static bool IsPlainVersion(string spec)
    {
        var text = (spec ?? "").Trim();

        if (text.Length == 0 || !char.IsAsciiDigit(text[0]))
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '.' && c != '-' && c != '+')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 解析版本范围
    /// </summary>
    /// <param name="spec">版本或者版本范围</param>
    /// <param name="range">解析结果</param>
    private static bool TryParseRange(string spec, out Range range)
    {
        range = default;

        var text = (spec ?? "").Trim();

        if (text.Length == 0)
        {
            return false;
        }

        // 裸版本号就是「大于等于这个版本」
        if (text[0] != '[' && text[0] != '(')
        {
            if (!IsPlainVersion(text))
            {
                return false;
            }

            range = new Range(text, true, "", false);
            return true;
        }

        if (text.Length < 3 || (text[^1] != ']' && text[^1] != ')'))
        {
            return false;
        }

        var minIncluded = text[0] == '[';
        var maxIncluded = text[^1] == ']';
        var body = text[1..^1];

        var parts = body.Split(',');

        if (parts.Length == 1)
        {
            // [1.0.0] 是精确版本
            var exact = parts[0].Trim();

            if (exact.Length == 0)
            {
                return false;
            }

            range = new Range(exact, true, exact, true);
            return true;
        }

        if (parts.Length != 2)
        {
            return false;
        }

        range = new Range(parts[0].Trim(), minIncluded, parts[1].Trim(), maxIncluded);

        return range.Min.Length > 0 || range.Max.Length > 0;
    }

    /// <summary>
    /// 版本在不在范围里
    /// </summary>
    /// <param name="version">版本</param>
    /// <param name="range">范围</param>
    private static bool InRange(string version, Range range)
    {
        // 范围里没写预发布就别用预发布版本顶上
        if (IsPrerelease(version) && !IsPrerelease(range.Min) && !IsPrerelease(range.Max))
        {
            return false;
        }

        if (range.Min.Length > 0)
        {
            var result = Compare(version, range.Min);

            if (result < 0 || (result == 0 && !range.MinIncluded))
            {
                return false;
            }
        }

        if (range.Max.Length > 0)
        {
            var result = Compare(version, range.Max);

            if (result > 0 || (result == 0 && !range.MaxIncluded))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 带不带预发布后缀
    /// </summary>
    /// <param name="version">版本</param>
    private static bool IsPrerelease(string version)
    {
        var text = (version ?? "").Trim();

        return text.Contains('-');
    }

    /// <summary>
    /// 拆成数字部分和预发布后缀
    /// </summary>
    /// <param name="version">版本</param>
    private static (int[] Numbers, string Pre) Split(string version)
    {
        var text = (version ?? "").Trim();

        var plus = text.IndexOf('+');

        if (plus >= 0)
        {
            text = text[..plus];
        }

        var dash = text.IndexOf('-');
        var pre = "";

        if (dash >= 0)
        {
            pre = text[(dash + 1)..];
            text = text[..dash];
        }

        var numbers = text
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var value) ? value : 0)
            .ToArray();

        return (numbers, pre);
    }

    /// <summary>
    /// 逐段比数字，短的一边补 0
    /// </summary>
    /// <param name="left">左边</param>
    /// <param name="right">右边</param>
    private static int CompareNumbers(int[] left, int[] right)
    {
        var length = Math.Max(left.Length, right.Length);

        for (var i = 0; i < length; i++)
        {
            var a = i < left.Length ? left[i] : 0;
            var b = i < right.Length ? right[i] : 0;

            if (a != b)
            {
                return a < b ? -1 : 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// 版本范围：下界、上界和两端是否包含，空字符串表示这一端不限
    /// </summary>
    private readonly record struct Range(string Min, bool MinIncluded, string Max, bool MaxIncluded);
}
