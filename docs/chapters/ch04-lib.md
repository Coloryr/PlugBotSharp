# 第四章：写一个运行库

运行库（`Type = Lib`）只提供类型和纯函数，不处理消息，也不参与启用 / 禁用。别的组件通过 `Deps` 依赖它，共用同一份类型。

例子：[DemoLib](../../Lib/DemoLib)（id `demolib`）。

## 1. 什么时候写运行库

- 几个插件 / 代理要共用一套模型或工具函数
- 想把平台相关的类型（比如 QQ 的消息体）单独放，给插件可选依赖
- 纯粹想给别的组件提供扩展点

**不要**在运行库里起线程、开端口、处理消息——它没有启用 / 禁用阶段。

## 2. 建工程

```
Lib/EchoLib/
  EchoLib.csproj
  Meta.cs
  LibMain.cs
  Models.cs
```

`csproj` 和插件一样，只是 `Meta.cs` 里 `Type` 不同。

## 3. 清单 Meta.cs

```csharp
public static class Meta
{
    public static MetaObj Info = new()
    {
        Name = "EchoLib",
        Id = "echolib",
        Version = "1.0.0",
        Description = "示例运行库",
        Type = ComponentType.Lib,
        Dlls = ["EchoLib.dll"],
        Deps = []
    };
}
```

## 4. 入口 LibMain

```csharp
using PlugBotSharp.Core.Lib;

namespace EchoLib;

public class LibMain : IBaseLib
{
    public bool Init(string path)      // Configs/echolib
    {
        return true;
    }

    public bool Load()
    {
        return true;
    }

    public bool Unload()
    {
        return true;
    }
}
```

只有 `Init → Load → Unload` 三个方法，没有 `Enable` / `Disable`。

## 5. 放什么进去

```csharp
namespace EchoLib;

/// <summary>
/// 给别的组件用的模型和纯函数
/// </summary>
public record EchoResult(string Text, int Code);

public static class EchoUtil
{
    public static EchoResult Ok(string text) => new(text, 0);

    public static string Normalize(string text) => text.Trim().ToLowerInvariant();
}
```

- 放**类型**（record / enum / 常量）和**纯函数**，别放运行状态
- 运行库里的 static 字段所有组件共用，容易变成隐藏的全局变量
- 需要配置或连接的话，应该写成插件 / 代理

## 6. 让别的组件用上

在插件 / 代理的 `Meta.cs` 里声明依赖，**不要**把 `EchoLib.dll` 抄进自己的 `Dlls`：

```csharp
Deps =
[
    new DepInfoObj { Type = DepType.Component, Id = "echolib", Version = ">=1.0.0" }
]
```

工程里正常 `ProjectReference` 引一下，编译期就能用；运行时宿主按 `Deps` 先加载运行库。

## 7. 打包和更新

```bash
dotnet build Lib/EchoLib/EchoLib.csproj
```

- 产物是 `EchoLib.zip`，装进面板后运行库只「加载」，没有「启用」
- 运行库改了之后，依赖它的组件要重载（面板「重载」或重启进程），否则还在用旧程序集
- 运行库被卸载时，依赖它的组件会先被卸载

## 8. 注意

- 没有启停，**别在里面起后台任务**
- 不要引用插件 / 代理的程序集，方向只能是「组件依赖运行库」
- 公共类型一旦发布，改名 / 改签名等于破坏性变更，依赖它的组件都要重编

---

上一章：[第三章 · 写一个代理](ch03-proxy.md) ｜ [返回目录](../README.md) ｜ 下一章：[第五章 · 自定义事件](ch05-events.md)
