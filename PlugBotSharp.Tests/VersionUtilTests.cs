using PlugBotSharp.Core.Nuget;

namespace PlugBotSharp.Tests;

public class VersionUtilTests
{
    [Theory]
    [InlineData("1.0.0", ">=1.0.0", true)]
    [InlineData("0.9.0", ">=1.0.0", false)]
    [InlineData("1.0.0", ">1.0.0", false)]
    [InlineData("1.0.1", ">1.0.0", true)]
    [InlineData("1.9.9", "<=2.0.0", true)]
    [InlineData("2.0.1", "<=2.0.0", false)]
    [InlineData("1.5.0", "1.0.0", true)]
    [InlineData("0.5.0", "1.0.0", false)]
    [InlineData("1.5.0", ">=1.0.0,<2.0.0", true)]
    [InlineData("2.0.0", ">=1.0.0,<2.0.0", false)]
    [InlineData("1.0.0-beta", ">=1.0.0", true)]
    [InlineData("1.0", "<=1.0.0", true)]
    [InlineData("1.0.0", "==1.0.0", true)]
    [InlineData("1.0.0", "==1.0.0.0", true)]
    public void Satisfies(string version, string range, bool expected)
    {
        Assert.Equal(expected, VersionUtil.Satisfies(version, range));
    }

    [Theory]
    [InlineData("1.0.0", null)]
    [InlineData("1.0.0", "")]
    [InlineData("1.0.0", "   ")]
    public void Empty_range_accepts_any_version(string version, string? range)
    {
        Assert.True(VersionUtil.Satisfies(version, range));
    }

    [Fact]
    public void Invalid_version_returns_false()
    {
        Assert.False(VersionUtil.Satisfies("abc", ">=1.0.0"));
    }
}
