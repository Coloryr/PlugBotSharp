using Xunit;

// 组件注册表是进程级静态状态，测试串行执行
[assembly: CollectionBehavior(DisableTestParallelization = true)]
