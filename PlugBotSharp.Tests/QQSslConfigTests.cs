using PlugBotSharp.Core.Configs;
using PlugBotSharp.QQProxy;

namespace PlugBotSharp.Tests;

/// <summary>
/// QQ 实例配置里的证书项
/// </summary>
public class QQSslConfigTests
{
    [Fact]
    public void Certificate_fields_are_saved_and_reloaded()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pbs-qq-ssl", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            File.WriteAllText(Path.Combine(folder, "config.json"),
                """{"AppId":"1","AppSecret":"s","Port":8080,"Type":2,"Certificate":"panel","CertificatePassword":"123456"}""");

            var instance = new ProxyInstance("qq");
            instance.Init(folder, Guid.NewGuid());
            Assert.True(instance.Load());

            var map = instance.ConfigMap();

            Assert.Equal("panel", ((ConfigEnumMap)map["Certificate"]).Value);
            Assert.Equal("123456", ((ConfigStringMap)map["CertificatePassword"]).Value);
            Assert.True(((ConfigStringMap)map["CertificatePassword"]).Secret);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Certificate_can_be_cleared()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pbs-qq-ssl", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var instance = new ProxyInstance("qq");
            instance.Init(folder, Guid.NewGuid());
            instance.Load();

            instance.UpdateMap(new Dictionary<string, ConfigMap>
            {
                ["Certificate"] = new ConfigEnumMap { Key = "Certificate", Value = "panel" }
            });

            Assert.Equal("panel", ((ConfigEnumMap)instance.ConfigMap()["Certificate"]).Value);

            instance.UpdateMap(new Dictionary<string, ConfigMap>
            {
                ["Certificate"] = new ConfigEnumMap { Key = "Certificate", Value = "" }
            });

            Assert.Equal("", ((ConfigEnumMap)instance.ConfigMap()["Certificate"]).Value);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
