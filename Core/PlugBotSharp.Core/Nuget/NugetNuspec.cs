using System.Xml.Linq;

namespace PlugBotSharp.Core.Nuget;

/// <summary>
/// 一条 NuGet 依赖：包 id 和版本范围
/// </summary>
/// <param name="Id">包 id</param>
/// <param name="Range">版本或者版本范围，原样来自 nuspec</param>
internal readonly record struct NugetDependency(string Id, string Range);

/// <summary>
/// 读 nuspec 里的依赖，按目标框架分组挑最合适的一组
/// </summary>
internal static class NugetNuspec
{
    /// <summary>
    /// 读 nuspec 里的依赖
    /// </summary>
    /// <param name="stream">nuspec 内容</param>
    /// <returns>依赖列表，没有依赖或者读不出来时是空列表</returns>
    public static List<NugetDependency> Read(Stream stream)
    {
        try
        {
            return Read(XDocument.Load(stream));
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// 从已经解析的 xml 里读依赖
    /// </summary>
    /// <param name="document">nuspec 的 xml</param>
    public static List<NugetDependency> Read(XDocument document)
    {
        var dependencies = document
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "dependencies");

        if (dependencies is null)
        {
            return [];
        }

        var groups = dependencies
            .Elements()
            .Where(e => e.Name.LocalName == "group")
            .ToList();

        // 老包没有分组，依赖直接挂在 dependencies 下面
        if (groups.Count == 0)
        {
            return ReadDependencies(dependencies);
        }

        // 先按目标框架挑，同分再看平台后缀，移动端的组排最后
        var best = groups
            .OrderByDescending(g => NugetTfm.Score(TfmOf(g)))
            .ThenByDescending(g => NugetTfm.PlatformBonus(TfmOf(g)))
            .First();

        return ReadDependencies(best);
    }

    /// <summary>
    /// 取分组的 targetFramework
    /// </summary>
    /// <param name="group">依赖分组</param>
    private static string TfmOf(XElement group)
    {
        return (string?)group.Attribute("targetFramework") ?? "";
    }

    /// <summary>
    /// 读一个节点下的 dependency
    /// </summary>
    /// <param name="parent">父节点</param>
    private static List<NugetDependency> ReadDependencies(XElement parent)
    {
        var result = new List<NugetDependency>();

        foreach (var item in parent.Elements().Where(e => e.Name.LocalName == "dependency"))
        {
            var id = ((string?)item.Attribute("id") ?? "").Trim();

            if (id.Length == 0)
            {
                continue;
            }

            var range = ((string?)item.Attribute("version") ?? "").Trim();

            result.Add(new NugetDependency(id, range));
        }

        return result;
    }
}
