using PlugBotSharp.Core.Configs;
using PlugBotSharp.OneBotLib;
using PlugBotSharp.OneBotProxy;

namespace PlugBotSharp.Tests;

/// <summary>
/// OneBot 实例配置：传输方式、客户端 / 服务端、收发开关
/// </summary>
public class OneBotConfigTests
{
    [Fact]
    public void Default_config_covers_transport_role_and_switches()
    {
        var instance = Create(out var folder);

        try
        {
            var map = instance.ConfigMap();

            Assert.Equal(
                new[] { "Protocol", "Transport", "Role", "Receive", "Send", "Url", "Host", "Port", "Token", "Certificate", "CertificatePassword", "ReconnectSeconds" },
                map.Keys.ToArray());

            Assert.Equal("WebSocket", ((ConfigEnumMap)map["Transport"]).Value);
            Assert.Equal("Client", ((ConfigEnumMap)map["Role"]).Value);
            Assert.True(((ConfigBoolMap)map["Receive"]).Value);
            Assert.True(((ConfigBoolMap)map["Send"]).Value);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Saved_config_survives_reload()
    {
        var instance = Create(out var folder);

        try
        {
            instance.UpdateMap(new Dictionary<string, ConfigMap>
            {
                ["Protocol"] = new ConfigEnumMap { Key = "Protocol", Value = "V12" },
                ["Transport"] = new ConfigEnumMap { Key = "Transport", Value = "Http" },
                ["Role"] = new ConfigEnumMap { Key = "Role", Value = "Server" },
                ["Receive"] = new ConfigBoolMap { Key = "Receive", Value = false },
                ["Send"] = new ConfigBoolMap { Key = "Send", Value = true },
                ["Port"] = new ConfigIntMap { Key = "Port", Value = 3200 }
            });

            var reloaded = new OneBotInstance("onebot");
            reloaded.Init(folder, Guid.NewGuid());
            Assert.True(reloaded.Load());

            var map = reloaded.ConfigMap();

            Assert.Equal("V12", ((ConfigEnumMap)map["Protocol"]).Value);
            Assert.Equal("Http", ((ConfigEnumMap)map["Transport"]).Value);
            Assert.Equal("Server", ((ConfigEnumMap)map["Role"]).Value);
            Assert.False(((ConfigBoolMap)map["Receive"]).Value);
            Assert.True(((ConfigBoolMap)map["Send"]).Value);
            Assert.Equal(3200, ((ConfigIntMap)map["Port"]).Value);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Send_only_instance_does_not_listen()
    {
        var instance = Create(out var folder);

        try
        {
            instance.UpdateMap(new Dictionary<string, ConfigMap>
            {
                ["Receive"] = new ConfigBoolMap { Key = "Receive", Value = false },
                ["Send"] = new ConfigBoolMap { Key = "Send", Value = true },
                ["Transport"] = new ConfigEnumMap { Key = "Transport", Value = "Http" },
                ["Role"] = new ConfigEnumMap { Key = "Role", Value = "Client" }
            });

            // 只发不发任何监听，Enable 直接成功
            Assert.True(instance.Enable());
            Assert.True(instance.Disable());
        }
        finally
        {
            instance.Unload();
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Empty_instance_is_rejected()
    {
        var instance = Create(out var folder);

        try
        {
            instance.UpdateMap(new Dictionary<string, ConfigMap>
            {
                ["Receive"] = new ConfigBoolMap { Key = "Receive", Value = false },
                ["Send"] = new ConfigBoolMap { Key = "Send", Value = false }
            });

            Assert.False(instance.Enable());
            Assert.Equal(OneBotVersion.V11, OneBotVersion.V11);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// 建一个指向临时目录的实例
    /// </summary>
    private static OneBotInstance Create(out string folder)
    {
        folder = Path.Combine(Path.GetTempPath(), "pbs-onebot-config", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        var instance = new OneBotInstance("onebot");
        instance.Init(folder, Guid.NewGuid());
        instance.Load();

        return instance;
    }
}