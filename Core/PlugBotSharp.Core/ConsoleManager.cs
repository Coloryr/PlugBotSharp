using PlugBotSharp.Core.Auth;

namespace PlugBotSharp.Core;

/// <summary>
/// 控制台指令：从标准输入读命令，输出运行状态
/// </summary>
internal static class ConsoleManager
{
    private static readonly Lock s_lock = new();
    private static Thread? s_thread;

    /// <summary>
    /// 启动控制台输入线程，输入结束后线程自己退出，不影响主线程
    /// </summary>
    public static void Start()
    {
        lock (s_lock)
        {
            if (s_thread is { IsAlive: true })
            {
                return;
            }

            s_thread = new Thread(Run)
            {
                Name = "Console",
                IsBackground = true
            };

            s_thread.Start();
        }
    }

    /// <summary>
    /// 启动横幅：版本、面板地址、组件和实例数量
    /// </summary>
    public static void PrintBanner()
    {
        var components = ComponentManager.ListAll();
        var instances = InstanceManager.List();

        WriteLine("");
        WriteLine("============================================================");
        WriteLine($" PlugBotSharp {typeof(BotManager).Assembly.GetName().Version}");
        WriteLine($" {BotLang.GetLang(BotLangKey.ConsolePanel)}: {HttpManager.Address}");
        WriteLine($" {BotLang.GetLang(BotLangKey.ConsoleComponents)}: {components.Count}    {BotLang.GetLang(BotLangKey.ConsoleInstances)}: {instances.Count}");
        WriteLine($" {BotLang.GetLang(BotLangKey.ConsoleHint)}");
        WriteLine("============================================================");
        WriteLine("");
    }

    /// <summary>
    /// 读取标准输入，输入不可用或者结束时直接退出线程
    /// </summary>
    private static void Run()
    {
        while (true)
        {
            string? line;

            try
            {
                line = Console.ReadLine();
            }
            catch
            {
                // 服务方式运行，没有可用标准输入
                return;
            }

            if (line is null)
            {
                // 输入结束，主线程继续等面板关闭
                return;
            }

            try
            {
                Execute(line);
            }
            catch (Exception e)
            {
                WriteLine(BotLang.GetLang(BotLangKey.ConsoleFailed, e.Message));
            }
        }
    }

    /// <summary>
    /// 执行一条指令
    /// </summary>
    /// <param name="line">用户输入</param>
    private static void Execute(string line)
    {
        // 前一行可能带 BOM，管道输入时要去掉
        var parts = line.Trim().TrimStart('\uFEFF').Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "help":
            case "?":
            case "h":
                WriteLine(BotLang.GetLang(BotLangKey.ConsoleHelp));
                break;

            case "status":
            case "st":
                PrintStatus();
                break;

            case "components":
            case "comp":
                PrintComponents(parts.Length > 1 ? parts[1] : null);
                break;

            case "instances":
            case "inst":
                PrintInstances();
                break;

            case "load":
            case "unload":
            case "enable":
            case "disable":
                RunComponent(parts, parts[0].ToLowerInvariant());
                break;

            case "clear":
            case "cls":
                Clear();
                break;

            case "stop":
            case "exit":
            case "quit":
                // 停机的日志会自己输出，这里不重复打印
                BotManager.Stop();
                break;

            default:
                WriteLine(BotLang.GetLang(BotLangKey.ConsoleUnknown, parts[0]));
                break;
        }
    }

    /// <summary>
    /// 组件的加载、卸载、启用、禁用
    /// </summary>
    /// <param name="parts">指令和参数</param>
    /// <param name="action">动作</param>
    private static void RunComponent(string[] parts, string action)
    {
        if (parts.Length < 2)
        {
            WriteLine(BotLang.GetLang(BotLangKey.ConsoleUsage, action + " <id>"));
            return;
        }

        var id = parts[1];

        // 成功时组件管理器自己会打日志，这里只在失败时补一句
        switch (action)
        {
            case "load":
                if (!ComponentLoader.LoadComponent(id))
                {
                    WriteLine(BotLang.GetLang(BotLangKey.ComponentLoadFail, id));
                }

                break;

            case "unload":
                if (!ComponentManager.UnloadComponent(id))
                {
                    WriteLine(BotLang.GetLang(BotLangKey.ComponentNotFound, id));
                }

                break;

            case "enable":
                if (!ComponentManager.EnableComponent(id))
                {
                    WriteLine(BotLang.GetLang(BotLangKey.ComponentEnableFail, id));
                }

                break;

            case "disable":
                if (!ComponentManager.DisableComponent(id))
                {
                    WriteLine(BotLang.GetLang(BotLangKey.ComponentNotSupported, id));
                }

                break;
        }
    }

    /// <summary>
    /// 输出运行状态
    /// </summary>
    private static void PrintStatus()
    {
        var components = ComponentManager.ListAll();
        var instances = InstanceManager.List();
        var uptime = DateTime.Now - BotManager.StartTime;

        WriteLine("");
        WriteLine(BotLang.GetLang(BotLangKey.ConsoleStatus));
        WriteLine($"  {Cell(BotLang.GetLang(BotLangKey.ConsoleVersion), 12)}{typeof(BotManager).Assembly.GetName().Version}");
        WriteLine($"  {Cell(BotLang.GetLang(BotLangKey.ConsoleUptime), 12)}{uptime:d\\.hh\\:mm\\:ss}");
        WriteLine($"  {Cell(BotLang.GetLang(BotLangKey.ConsoleStartAt), 12)}{BotManager.StartTime:yyyy-MM-dd HH:mm:ss}");
        WriteLine($"  {Cell(BotLang.GetLang(BotLangKey.ConsolePanel), 12)}{HttpManager.Address}");
        WriteLine($"  {Cell(BotLang.GetLang(BotLangKey.ConsoleAccounts), 12)}{AccountManager.Count}");
        WriteLine($"  {Cell(BotLang.GetLang(BotLangKey.ConsoleComponents), 12)}{components.Count}    " +
                  $"loaded {components.Count(x => x.Assembly is not null)} / enabled {components.Count(x => ComponentManager.GetState(x.Meta.Id) == "enabled")}");
        WriteLine($"  {Cell(BotLang.GetLang(BotLangKey.ConsoleInstances), 12)}{instances.Count}    " +
                  $"loaded {instances.Count(x => x.Loaded)} / enabled {instances.Count(x => x.Enabled)}");
        WriteLine("");
    }

    /// <summary>
    /// 输出组件列表
    /// </summary>
    /// <param name="type">按类型过滤，空表示全部</param>
    private static void PrintComponents(string? type)
    {
        var items = ComponentManager.ListAll();

        if (!string.IsNullOrWhiteSpace(type))
        {
            items = items
                .Where(x => x.Meta.Type.ToString().Equals(type, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        WriteLine("");
        WriteLine($"{BotLang.GetLang(BotLangKey.ConsoleComponents)} ({items.Count})");

        if (items.Count == 0)
        {
            WriteLine("  " + BotLang.GetLang(BotLangKey.ConsoleEmpty));
            WriteLine("");
            return;
        }

        WriteLine("  " + Cell("id", 20) + Cell("type", 10) + Cell("state", 12) + Cell("version", 10) + "name");

        foreach (var item in items)
        {
            WriteLine("  "
                + Cell(item.Meta.Id, 20)
                + Cell(item.Meta.Type.ToString(), 10)
                + Cell(StateLabel(ComponentManager.GetState(item.Meta.Id)), 12)
                + Cell(item.Meta.Version, 10)
                + item.Meta.Name);
        }

        WriteLine("");
    }

    /// <summary>
    /// 输出实例列表
    /// </summary>
    private static void PrintInstances()
    {
        var items = InstanceManager.List();

        WriteLine("");
        WriteLine($"{BotLang.GetLang(BotLangKey.ConsoleInstances)} ({items.Count})");

        if (items.Count == 0)
        {
            WriteLine("  " + BotLang.GetLang(BotLangKey.ConsoleEmpty));
            WriteLine("");
            return;
        }

        WriteLine("  " + Cell("uuid", 38) + Cell("proxy", 12) + Cell("name", 18) + "state");

        foreach (var item in items)
        {
            WriteLine("  "
                + Cell(item.Uuid.ToString(), 38)
                + Cell(item.Proxy, 12)
                + Cell(item.Name, 18)
                + StateLabel(item.Loaded ? item.Enabled ? "enabled" : "disabled" : "unloaded"));
        }

        WriteLine("");
    }

    /// <summary>
    /// 状态文案
    /// </summary>
    /// <param name="state">状态值</param>
    private static string StateLabel(string state)
    {
        return state switch
        {
            "enabled" => BotLang.GetLang(BotLangKey.ConsoleStateEnabled),
            "disabled" => BotLang.GetLang(BotLangKey.ConsoleStateDisabled),
            "loaded" => BotLang.GetLang(BotLangKey.ConsoleStateLoaded),
            "blocked" => BotLang.GetLang(BotLangKey.ConsoleStateBlocked),
            _ => BotLang.GetLang(BotLangKey.ConsoleStateUnloaded)
        };
    }

    /// <summary>
    /// 按显示宽度补空格，中文算两个字符宽
    /// </summary>
    /// <param name="text">文本</param>
    /// <param name="width">目标宽度</param>
    private static string Cell(string? text, int width)
    {
        text ??= "";

        var current = 0;

        foreach (var c in text)
        {
            current += c > 0x7F ? 2 : 1;
        }

        return current >= width ? text + " " : text + new string(' ', width - current);
    }

    /// <summary>
    /// 清屏，输出被重定向时忽略
    /// </summary>
    private static void Clear()
    {
        try
        {
            Console.Clear();
        }
        catch
        {
            // 没有控制台时忽略
        }
    }

    /// <summary>
    /// 输出一行
    /// </summary>
    /// <param name="text">内容</param>
    private static void WriteLine(string text)
    {
        try
        {
            Console.WriteLine(text);
        }
        catch
        {
            // 以服务方式运行、标准输出不可用时忽略
        }
    }
}