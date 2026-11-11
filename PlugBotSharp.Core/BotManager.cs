using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Plugin;
using PlugBotSharp.Core.Proxy;
using System.Collections.Concurrent;

namespace PlugBotSharp.Core;

public static class BotManager
{
    internal static ConcurrentDictionary<string, PluginAssembly> Plugins = [];
    internal static ConcurrentDictionary<string, ProxyAssembly> Proxys = [];

    internal static string ConfigFile { get; private set; }
    internal static string InstancesPath { get; private set; }
    internal static string PluginsPath { get; private set; }
    internal static string ConfigsPath { get; private set; }
    internal static string ProxysPath { get; private set; }

    public static void Init(string path)
    {
        ConfigSave.Init();

        ConfigFile = Path.Combine(path, "Config.json");

        InstancesPath = Path.Combine(path, "Instances");
        PluginsPath = Path.Combine(path, "Plugins");
        ConfigsPath = Path.Combine(path, "Configs");
        ProxysPath = Path.Combine(path, "Proxys");

        if (!Directory.Exists(InstancesPath))
        {
            Directory.CreateDirectory(InstancesPath);
        }
        if (!Directory.Exists(PluginsPath))
        {
            Directory.CreateDirectory(PluginsPath);
        }
        if (!Directory.Exists(ConfigsPath))
        {
            Directory.CreateDirectory(ConfigsPath);
        }
        if (!Directory.Exists(ProxysPath))
        {
            Directory.CreateDirectory(ProxysPath);
        }
    }

    public static void Load()
    { 
        
    }
}
