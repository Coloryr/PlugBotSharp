using PlugBotSharp.Core;
using System.Runtime.CompilerServices;
using PlugBotSharp.Api;

// 组件注册表是进程级静态状态，测试串行执行
[assembly: CollectionBehavior(DisableTestParallelization = true)]

/// <summary>
/// 测试进程启动时先把宿主实现注入 Api，有的用例不走 ComponentTestBase
/// </summary>
internal static class TestBootstrap
{
    [ModuleInitializer]
    internal static void Init()
    {
        ApiHost.Current = new HostApi();
    }
}
