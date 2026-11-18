using NLog;
using NLog.Config;
using NLog.Targets;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Http;
using PlugBotSharp.Core.Plugin;
using PlugBotSharp.Core.Proxy;
using System.Collections.Concurrent;

namespace PlugBotSharp.Core;

public static class BotManager
{
    public static Lang Lang { get; private set; } = Lang.zh_cn;

    internal static Logger Logger;

    internal static ConcurrentDictionary<string, PluginAssembly> Plugins = [];
    internal static ConcurrentDictionary<string, ProxyAssembly> Proxys = [];

    internal static CoreHttpServer? HttpServer { get; private set; }

    internal static string ConfigFile { get; private set; }
    internal static string InstancesPath { get; private set; }
    internal static string PluginsPath { get; private set; }
    internal static string ConfigsPath { get; private set; }
    internal static string ProxysPath { get; private set; }
    internal static string LogsPath { get; private set; }

    public static void Init(string path)
    {
        ConfigSave.Init();

        ConfigFile = Path.Combine(path, "Config.json");

        InstancesPath = Path.Combine(path, "Instances");
        PluginsPath = Path.Combine(path, "Plugins");
        ConfigsPath = Path.Combine(path, "Configs");
        ProxysPath = Path.Combine(path, "Proxys");
        LogsPath = Path.Combine(path, "Logs");

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
        if (!Directory.Exists(LogsPath))
        {
            Directory.CreateDirectory(LogsPath);
        }
    }

    public static void Load()
    {
        var config = new LoggingConfiguration();
        var fileTarget = new FileTarget("sharedFile")
        {
            FileName = "${basedir}/Logs/${date:format=yyyy-MM-dd}.log",
            Layout = "[${longdate}][${level:uppercase=true}][${logger}]${message}${newline}",
            KeepFileOpen = true,
            CreateDirs = true
        };

        config.AddRule(LogLevel.Debug, LogLevel.Fatal, fileTarget);

        LogManager.Configuration = config;

        Logger = LogManager.GetLogger("BotManager");

        Logger.Info(BotLang.GetLang(BotLangKey.Init));

        var appConfig = LoadConfig();

        if (appConfig.Port == 0)
        {
            Logger.Warn("Admin panel disabled");
            return;
        }

        var server = new CoreHttpServer();

        if (server.Start(appConfig.Port).GetAwaiter().GetResult())
        {
            HttpServer = server;
        }
        else
        {
            Logger.Error("Admin panel start fail");
        }
    }

    /// <summary>
    /// 停止
    /// </summary>
    public static void Stop()
    {
        if (HttpServer is { } server)
        {
            HttpServer = null;
            server.Stop().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// 读取配置文件，不存在时生成默认配置
    /// </summary>
    private static ConfigObj LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                var obj = JsonUtils.ToObj(File.ReadAllText(ConfigFile), SourceGenerationContext.Default.ConfigObj);

                if (obj != null)
                {
                    return obj;
                }
            }
            else
            {
                var obj = new ConfigObj();

                File.WriteAllText(ConfigFile, JsonUtils.ToString(obj, SourceGenerationContext.Default.ConfigObj));

                return obj;
            }
        }
        catch (Exception e)
        {
            Logger.Error("Config read error", e);
        }

        return new ConfigObj();
    }
}