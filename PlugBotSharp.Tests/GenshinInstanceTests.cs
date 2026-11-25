using PlugBotSharp.Core.Configs;
using PlugBotSharp.GenshinProxy;

namespace PlugBotSharp.Tests;

/// <summary>
/// gsuid_core 代理的实例配置
/// </summary>
public class GenshinInstanceTests
{
    [Fact]
    public void Default_config_exposes_url_botid_and_token()
    {
        var folder = CreateFolder();

        try
        {
            var instance = new GenshinInstance("genshin");
            instance.Init(folder, Guid.NewGuid());
            Assert.True(instance.Load());

            var map = instance.ConfigMap();

            Assert.Equal(new[] { "Url", "BotId", "Token", "ReconnectSeconds" }, map.Keys.ToArray());
            Assert.Equal("ws://127.0.0.1:8765", ((ConfigStringMap)map["Url"]).Value);
            Assert.Equal("PlugBotSharp", ((ConfigStringMap)map["BotId"]).Value);
            Assert.True(((ConfigStringMap)map["Token"]).Secret);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Saved_config_survives_reload()
    {
        var folder = CreateFolder();

        try
        {
            var instance = new GenshinInstance("genshin");
            instance.Init(folder, Guid.NewGuid());
            instance.Load();

            instance.UpdateMap(new Dictionary<string, ConfigMap>
            {
                ["Url"] = new ConfigStringMap { Key = "Url", Value = "ws://10.0.0.2:9000" },
                ["BotId"] = new ConfigStringMap { Key = "BotId", Value = "MyBot" },
                ["Token"] = new ConfigStringMap { Key = "Token", Value = "secret" },
                ["ReconnectSeconds"] = new ConfigIntMap { Key = "ReconnectSeconds", Value = 30 }
            });

            var reloaded = new GenshinInstance("genshin");
            reloaded.Init(folder, Guid.NewGuid());
            Assert.True(reloaded.Load());

            var map = reloaded.ConfigMap();

            Assert.Equal("ws://10.0.0.2:9000", ((ConfigStringMap)map["Url"]).Value);
            Assert.Equal("MyBot", ((ConfigStringMap)map["BotId"]).Value);
            Assert.Equal("secret", ((ConfigStringMap)map["Token"]).Value);
            Assert.Equal(30, ((ConfigIntMap)map["ReconnectSeconds"]).Value);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// 建一个临时实例目录
    /// </summary>
    private static string CreateFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pbs-genshin", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        return folder;
    }
}
