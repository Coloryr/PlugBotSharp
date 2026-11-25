using PlugBotSharp.Core.Configs;
using PlugBotSharp.QQProxy;

namespace PlugBotSharp.Tests;

/// <summary>
/// 每个群每天的主动消息配额
/// </summary>
public class ActiveQuotaTests
{
    [Fact]
    public void Quota_stops_at_the_limit_and_survives_restart()
    {
        var folder = CreateFolder();

        try
        {
            var quota = new ActiveQuota(folder);

            for (var i = 0; i < ActiveQuota.Limit; i++)
            {
                Assert.True(quota.CanSend("group"));
                quota.Add("group");
            }

            Assert.False(quota.CanSend("group"));
            Assert.True(quota.CanSend("other"));

            // 进程重启后读回来还是满的
            var reloaded = new ActiveQuota(folder);

            Assert.False(reloaded.CanSend("group"));
            Assert.Equal(ActiveQuota.Limit, reloaded.Get("group"));
            Assert.Equal(0, reloaded.Get("other"));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Quota_resets_on_a_new_day()
    {
        var folder = CreateFolder();

        try
        {
            var data = new QuotaFileObj
            {
                Date = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"),
                Groups = new Dictionary<string, int> { ["group"] = ActiveQuota.Limit }
            };

            File.WriteAllText(Path.Combine(folder, "quota.json"), JsonUtils.ToString(data, JsonContext.Default.QuotaFileObj));

            var quota = new ActiveQuota(folder);

            Assert.True(quota.CanSend("group"));
            Assert.Equal(0, quota.Get("group"));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// 建临时实例目录
    /// </summary>
    private static string CreateFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pbs-qq-quota", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(folder);

        return folder;
    }
}
