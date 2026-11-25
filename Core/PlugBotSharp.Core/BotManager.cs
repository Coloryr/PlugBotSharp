using NLog;
using NLog.Config;
using NLog.Targets;
using PlugBotSharp.Core.Auth;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Http;
using PlugBotSharp.Core.Nuget;

namespace PlugBotSharp.Core;

/// <summary>
/// 框架入口
/// </summary>
public static class BotManager
{
    /// <summary>
    /// 当前语言
    /// </summary>
    public static Lang Lang { get; private set; } = Lang.zh_cn;

    /// <summary>
    /// 主日志记录器
    /// </summary>
    internal static Logger Logger { get; set; } = LogManager.GetLogger("BotManager");

    /// <summary>
    /// 启动时间
    /// </summary>
    internal static DateTime StartTime { get; private set; } = DateTime.Now;

    /// <summary>
    /// 主配置文件
    /// </summary>
    internal static string ConfigFile { get; private set; } = "";

    /// <summary>
    /// 实例目录
    /// </summary>
    internal static string InstancesPath { get; private set; } = "";

    /// <summary>
    /// 组件目录
    /// </summary>
    internal static string ComponentsPath { get; private set; } = "";

    /// <summary>
    /// NuGet 包目录
    /// </summary>
    internal static string NugetsPath { get; private set; } = "";

    /// <summary>
    /// 配置目录
    /// </summary>
    internal static string ConfigsPath { get; private set; } = "";

    /// <summary>
    /// 日志目录
    /// </summary>
    internal static string LogsPath { get; private set; } = "";

    /// <summary>
    /// 证书目录
    /// </summary>
    internal static string CertsPath { get; private set; } = "";

    private static readonly Lock s_stopLock = new();
    private static readonly ManualResetEventSlim s_stopEvent = new(false);

    private static bool s_loaded;
    private static bool s_stopped;
    private static bool s_logInited;

    /// <summary>
    /// 初始化框架：准备目录并读取启用状态
    /// </summary>
    /// <param name="path">运行目录</param>
    public static void Init(string path)
    {
        StartTime = DateTime.Now;

        ConfigFile = Path.Combine(path, "Config.json");
        InstancesPath = Path.Combine(path, "Instances");
        ComponentsPath = Path.Combine(path, "Components");
        NugetsPath = Path.Combine(path, "Nugets");
        ConfigsPath = Path.Combine(path, "Configs");
        LogsPath = Path.Combine(path, "Logs");
        CertsPath = Path.Combine(path, "Certs");

        foreach (var dir in new[] { InstancesPath, ComponentsPath, NugetsPath, ConfigsPath, LogsPath, CertsPath })
        {
            Directory.CreateDirectory(dir);
        }

        CertStore.Init(CertsPath);
        ConfigSave.Init();
        StateManager.Init();
        NugetManager.Init(NugetsPath);
        AccountManager.Init(ConfigsPath);
        SessionManager.Init(ConfigsPath);
    }

    /// <summary>
    /// 启动：日志、状态、组件与实例，最后启动管理面板并阻塞在这里，直到进程退出
    /// </summary>
    public static void Load()
    {
        if (s_loaded)
        {
            return;
        }

        s_loaded = true;

        InitLog();
        Logger.Info(BotLang.GetLang(BotLangKey.Init));

        var config = LoadConfig();

        // 面板必须启用，端口为 0 直接退出
        if (config.Port == 0)
        {
            Logger.Error(BotLang.GetLang(BotLangKey.PanelRequired));
            Environment.Exit(1);
        }

        StateManager.Load();

        NugetManager.Scan();

        // 加载组件，再启用组件、创建并启用实例
        ComponentLoader.LoadAll(ComponentsPath);
        ComponentManager.Start();
        InstanceManager.Start();

        // 面板最后启动，这时组件和实例都已经就绪
        HttpManager.Start(config.Port, config.Certificate, config.CertificatePassword);

        // 控制台输出状态并开始接收指令
        ConsoleManager.PrintBanner();
        ConsoleManager.Start();

        // 阻塞在面板上，进程保持运行，直到 Ctrl+C 或者控制台 stop
        HttpManager.WaitForShutdown();

        // 统一在这里收尾，控制台指令已经停过一次时这里只会等待它完成
        Stop();

        Logger.Info(BotLang.GetLang(BotLangKey.Stopped));
    }

    /// <summary>
    /// 停止
    /// </summary>
    public static void Stop()
    {
        lock (s_stopLock)
        {
            if (!s_stopped)
            {
                s_stopped = true;

                try
                {
                    Logger.Info(BotLang.GetLang(BotLangKey.Stopping));

                    SessionManager.Save();
                    StateRegistry.Flush();
                    HttpManager.Stop();
                    InstanceManager.Stop();
                    ComponentManager.Stop();
                    StateManager.Save();
                }
                finally
                {
                    s_stopEvent.Set();
                }
            }
        }

        // 别的线程正在收尾时，这里等它完成，避免主线程提前退出把收尾线程杀掉
        s_stopEvent.Wait();
    }

    /// <summary>
    /// 初始化文件日志
    /// </summary>
    private static void InitLog()
    {
        if (s_logInited)
        {
            return;
        }

        s_logInited = true;

        var config = new LoggingConfiguration();
        var fileTarget = new FileTarget("sharedFile")
        {
            FileName = Path.Combine(LogsPath, "${date:format=yyyy-MM-dd}.log"),
            // 带上异常堆栈，否则出错时只有一行消息
            Layout = "[${longdate}][${level:uppercase=true}][${logger}]${message}${onexception:${newline}${exception:format=ToString}}",
            KeepFileOpen = true,
            CreateDirs = true
        };

        // 控制台也输出一份，方便直接看运行情况
        var consoleTarget = new ColoredConsoleTarget("console")
        {
            Layout = "[${level:uppercase=true}] ${message}${onexception:${newline}${exception:format=ToString}}"
        };

        config.AddRule(LogLevel.Debug, LogLevel.Fatal, fileTarget);
        config.AddRule(LogLevel.Info, LogLevel.Fatal, consoleTarget);

        LogManager.Configuration = config;

        Logger = LogManager.GetLogger("BotManager");
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
            Logger.Error(e, BotLang.GetLang(BotLangKey.ErrorConfigRead));
        }

        return new ConfigObj();
    }
}