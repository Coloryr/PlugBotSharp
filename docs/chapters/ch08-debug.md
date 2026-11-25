# 第八章：远程调试

组件跑在宿主的 `AssemblyLoadContext` 里，出问题没法直接断点（除非本地附加进程）。这一章是远程 / 无界面环境下的调试手段。

## 1. 手段总览

| 手段 | 看什么 | 怎么用 |
| --- | --- | --- |
| 日志 | 事件流、异常栈 | `Logs/yyyy-MM-dd.log`（Debug 级）、控制台（Info 级） |
| 面板 | 组件 / 实例状态、自定义状态、配置 | 浏览器打开面板 |
| 控制台 | 加载 / 启用 / 卸载、组件实例列表 | 进程标准输入 |
| `ComponentApi` | 其他组件的状态、配置 | 代码里查 |
| 自建 http 探针 | 自定义的自检信息 | `register.RegisterHttpPath` + curl |

## 2. 日志

宿主用 NLog，写在运行目录的 `Logs/yyyy-MM-dd.log`，级别 `Debug` 起；控制台只输出 `Info` 起。

```csharp
using NLog;

private static readonly Logger Logger = LogManager.GetLogger("Echo");

Logger.Info("收到消息");
Logger.Warn("配置缺失，用默认值");
Logger.Error(e, "处理失败");
```

- 组件名随便起，日志里只看得到文本
- 异常一定要把 `e` 传进去，否则只剩一句描述
- 调试期间可以在关键路径加 `Debug`，正常跑时看不到，排查时把日志文件拉下来看

## 3. 面板

- 「组件」页：每个组件的状态（运行中 / 已禁用 / 已加载 / 未加载 / 依赖缺失），能重载、禁用、卸载
- 「实例」页：实例连没连上、启用状态
- 「概览」页：自定义状态卡片，计数有没有在动一眼就知道
- 设置弹窗：改配置立刻生效，不用重启

面板默认只监听 `127.0.0.1`，远程机器上用 SSH 隧道最省事：

```bash
ssh -L 10000:127.0.0.1:10000 user@server
# 然后本地浏览器打开 http://127.0.0.1:10000
```

想直接暴露出去就把 `Certs` 配好、走 https 加反向代理，**不要**把 http 面板挂到公网。

## 4. 控制台

有标准输入时可以直接敲：

| 指令 | 说明 |
| --- | --- |
| `status` | 版本、运行时长、面板地址、组件 / 实例数 |
| `components [type]` | 组件列表，可以筛 `plugin` / `proxy` / `lib` |
| `instances` | 实例列表 |
| `load <id>` / `unload <id>` | 加载 / 卸载组件 |
| `enable <id>` / `disable <id>` | 启用 / 禁用组件 |

以服务方式跑、没有标准输入时控制台线程会自动退出，不影响进程。

## 5. 代码里自检

`ComponentApi` 能拿到宿主视角的信息：

```csharp
var me = ComponentApi.GetComponentState("echo");        // enabled / disabled / loaded / unloaded
var all = ComponentApi.GetComponents();                  // 每个组件的状态、是否加载
var instances = ComponentApi.GetInstances();             // 实例、所属代理、启用状态

foreach (var item in instances)
{
    Logger.Info($"{item.Proxy} {item.Name} enabled={item.Enabled}");
}
```

适合做「等依赖就绪再干活」：比如插件启动时依赖的代理还没启用，就隔几秒查一次。

## 6. 自建 http 探针

注册一个路径，返回自己的自检信息，远程 `curl` 一下就行：

```csharp
public void RegisterEventHook(IRegister register)
{
    register.RegisterHttpPath(HandleDebug);
}

private static async Task HandleDebug(HttpContext ctx)
{
    ctx.Response.ContentType = "application/json";

    await ctx.Response.WriteAsync("""{"ok":true,"queued":0}""");
}
```

```bash
curl http://127.0.0.1:10000/api/echo
```

- 插件 / 代理的路径是 `/api/{组件id}`，实例是 `/api/{组件id}/{uuid}`
- 这个路径是**公开**的（不需要登录），别把密码、token 放进去
- 回调里抛异常宿主会记日志并返回 500

## 7. 排查顺序

问题通常出在这条链的某一环，从头往下查：

| 顺序 | 查什么 | 怎么看 |
| --- | --- | --- |
| 1 | 组件加载了吗 | 面板「组件」页，或 `ComponentApi.GetComponentState` |
| 2 | 启用了吗 | 同上，`enabled` 才算在跑 |
| 3 | 实例在跑吗 | 面板「实例」页 / `GetInstances` |
| 4 | 消息到了吗 | 状态计数（`recv`）、日志里的接收记录 |
| 5 | 插件回了吗 | 状态计数（`replies`）、日志 |
| 6 | 发出去了吗 | 代理的发送日志、平台侧有没有收到 |
| 7 | 依赖满足吗 | 组件列表里的「依赖缺失」，或者日志里的 `blocked` |

## 8. 断点调试

远程也能打断点，前提是三件事都满足：**组件是 Debug 构建**、**有符号文件**、**调试器关了「仅我的代码」**。

### 准备符号

- Debug 构建默认会生成 `.pdb`，构建组件时会自动打进 zip，和 dll 一起解到 `Components/{id}/`
- Release 构建默认不带 pdb；想发布版也能调试，在组件 csproj 里把 pdb 嵌进 dll：

```xml
<PropertyGroup>
  <DebugType>embedded</DebugType>
</PropertyGroup>
```

- 重装组件后要**重新附加**调试器，断点才认新模块

### 关掉「仅我的代码」

组件的代码不在宿主的解决方案里，开着 JMC 调试器会把它们当外部代码跳过，断点变空心。

| IDE | 位置 |
| --- | --- |
| Visual Studio | 工具 → 选项 → 调试 → 常规 → 取消「启用“仅我的代码”」 |
| Rider | Settings → Build, Execution, Deployment → Debugger → 取消 `Enable "Just My Code"` |

### 本地附加

1. 启动 `PlugBotSharp.Run`（面板起来、组件已加载）
2. IDE 里「附加到进程」，选 `PlugBotSharp.Run`（或 `dotnet` 托管的那个进程）
3. 打开组件源码，在 `HandleMessage` 里下断点
4. 发一条消息触发它

### 远程附加

- **Visual Studio**：把远程调试器（msvsmon，和 VS 同版本）拷到服务器跑起来，调试 → 附加到进程 → 连接类型选远程 → 填 `服务器:4026`，再选 `PlugBotSharp.Run`
- **Rider**：用 SSH 远程调试，配好服务器的 dotnet 和进程，直接“Attach to Remote Process”
- **VS Code**：`launch.json` 里用 `pipeTransport` 连远端 `vsdbg`

不管哪种方式，组件都要在**附加之前**已经加载，否则调试器看不到模块。

### 断点打不上 / 空心

| 现象 | 原因 |
| --- | --- |
| 断点空心，提示没有符号 | 组件包里没有 pdb，或者 pdb 和 dll 版本不一致 |
| 断点被跳过 | 「仅我的代码」没关 |
| 断点能打但不停 | Release 优化、代码改了没重新打包，或者根本没走这条路径 |
| 附加后所有断点失效 | 组件被重载 / 卸载过，旧模块没了，重新附加 + 重新下断点 |
| 断点停在别处 | 同一个方法被多个组件调用，看调用栈确认 |

### 注意

- 宿主和组件是**不同程序集**，跨组件调用时调用栈里能看到 `AssemblyLoadContext` 的边界
- 热重载不支持：改代码要重新打包、重装、重载组件，然后重新附加
- 调试时别开「中断所有进程」，宿主是单进程多线程，停住会影响所有组件

## 9. 常见远程问题

| 现象 | 原因 |
| --- | --- |
| 面板打不开 | 只监听 127.0.0.1，用隧道或反向代理 |
| 日志没内容 | 看的是 `Info` 级，调试信息在 `Debug` 级，去 `Logs` 文件里看 |
| 改了配置没生效 | 保存后没重载，面板会自动调，手动改文件就得点「重载」 |
| 重启后状态丢了 | 状态本来就是内存态，要持久化写配置文件 |
| 组件卸载后还在跑 | 有线程 / 定时器没停，`Disable` / `Unload` 里要收干净 |

---

上一章：[第七章 · 自定义数据](ch07-state.md) ｜ [返回目录](../README.md) ｜ 下一章：[第九章 · 依赖库和运行库](ch09-deps.md)
