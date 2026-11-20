using NLog;
using PlugBotSharp.Core;

namespace PlugBotSharp.Tests;

/// <summary>
/// 组件测试基类：准备临时目录、日志，结束后清理全局状态
/// </summary>
public abstract class ComponentTestBase : IDisposable
{
    protected string Root { get; }

    protected ComponentTestBase()
    {
        Root = Path.Combine(Path.GetTempPath(), "plugbotsharp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);

        FakePlugin.UnloadOrder.Clear();

        BotManager.Logger = LogManager.GetLogger("Tests");
        BotManager.Init(Root);
    }

    public void Dispose()
    {
        ComponentManager.UnloadAll();

        ComponentManager.Plugins.Clear();
        ComponentManager.Proxys.Clear();
        ComponentManager.Libs.Clear();

        try
        {
            Directory.Delete(Root, true);
        }
        catch
        {
            // 清理失败不影响测试结果
        }
    }
}
