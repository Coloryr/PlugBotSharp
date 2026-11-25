# PlugBotSharp 开发文档

写给要**写组件**和**改宿主**的人。装组件、用面板、看协议细节见 [根目录 README](../README.md)。

## 目录

| 章节 | 内容 |
| --- | --- |
| **第一章 · 快速上手**（本页） | 选插件还是代理、最小插件、最小代理、生命周期 |
| [第二章 · 完整插件制作](./chapters/ch02-plugin.md) | 从空目录建工程，到消息解析、消息发送、配置、状态、打包安装 |
| [第三章 · 写一个代理](./chapters/ch03-proxy.md) | 收消息、Dispatch、发消息、实例配置、打包测试 |
| [第四章 · 写一个运行库](./chapters/ch04-lib.md) | Lib 组件怎么写、怎么给别的组件用 |
| [第五章 · 事件系统](./chapters/ch05-events.md) | `IRegister` 全貌：消息 / 插件 / 代理事件、自定义事件、拦截、句柄 |
| [第六章 · 配置文件](./chapters/ch06-config.md) | ConfigMap 全类型、落盘、热重载 |
| [第七章 · 自定义数据](./chapters/ch07-state.md) | 单值 / 键值状态、面板监控卡片 |
| [第八章 · 远程调试](./chapters/ch08-debug.md) | 日志、面板、控制台、断点调试、排错顺序 |
| [第九章 · 依赖库和运行库](./chapters/ch09-deps.md) | 组件依赖、NuGet 子依赖、native 库 |
| [第十章 · 打包与发布](./chapters/ch10-package.md) | 打包、安装、更新、发布自查 |
| [第十一章 · ComponentApi 进阶](./chapters/ch11-componentapi.md) | 查 / 管别的组件和实例、健康检查、给自己做面板 |
| [第十二章 · RegisterHttpPath 与代理配合](./chapters/ch12-http.md) | 对外接口、自建服务器（StartServer）、鉴权、接外部系统再走代理 |

## 先选：写插件还是写代理

| 你要做的事 | 写什么 | 为什么 |
| --- | --- | --- |
| 收到消息后处理它：回复、查数据、发图、做指令 | **插件 Plugin** | 插件只认统一消息模型，不用管消息是从 QQ、OneBot 还是别的平台来的 |
| 接一个平台：登录、收事件、发消息、签名、断线重连 | **代理 Proxy** | 代理把平台协议翻译成统一消息，插件一行都不用改 |
| 只把模型 / 工具类提供给别人，自己不处理消息 | **运行库 Lib** | 只有 `Init` / `Load` / `Unload`，不参与启用 / 禁用 |

```
外部平台 ──协议报文──▶ 代理 Proxy ──ProxyMessage──▶ 宿主分发 ──▶ 插件 Plugin
     ▲                     │                                   │
     └────── 平台报文 ◀─────┴────── BaseMessage ◀──────────────┘
```

判断方法很简单：

- **只是处理消息内容**，不关心消息从哪来 → 写插件，例子是 [DemoPlugin](../Plugin/DemoPlugin)
- **要接一个新平台 / 新协议**，或者把别的系统的消息转进来 → 写代理，例子是 [ProxyDemo](../Proxy/ProxyDemo)
- **别的组件要用你的类型，但你自己不发不收消息** → 写运行库，例子是 [DemoLib](../Lib/DemoLib)

代理和插件之间只有一条约定：代理把 `ProxyMessage` 注入进来，插件返回 `BaseMessage`，代理再把它翻译成平台能发的格式。所以：

- 插件里**不要**出现平台相关的代码（QQ 的 `openid`、OneBot 的 `user_id` 这类字段）
- 代理里**不要**写业务逻辑，收到什么就交出去，插件返回什么就发什么
- 多个代理同时在线时，插件收到的是同一套模型，回复交给发起这次分发的实例发回

## 快速上手

### 插件

最小插件（完整例子见 [plugin.md](./reference/plugin.md)）：

```csharp
public class PluginMain : IBasePlugin
{
    public bool Init(string path) => true;
    public bool Load() => true;
    public bool Unload() => true;
    public bool Enable() => true;
    public bool Disable() => true;
    public Dictionary<string, ConfigMap> ConfigMap() => new();
    public void UpdateMap(Dictionary<string, ConfigMap> data) { }
    public void Reload() { }

    public void RegisterEventHook(IRegister register)
    {
        // 收到消息就回一句
        register.SubscribeMessage(message => message.Message is TextMessage text
            ? [new TextMessage("收到：" + text.Text)]
            : []);
    }
}
```

这个类的成员，宿主和面板按下面的时机调：

| 成员 | 什么时候调 | 该写什么 |
| --- | --- | --- |
| `Init(path)` | 加载时一次 | 记住 `Configs/{组件id}`，把配置文件读进来 |
| `Load()` | 加载时 | 初始化资源；返回 `false` 算加载失败，组件不会被启用 |
| `RegisterEventHook(register)` | `Load` 末尾一次 | 所有注册 / 订阅都写这里，出了这个阶段注册器就关闭 |
| `Enable()` / `Disable()` | 面板启用 / 禁用 | 起停后台任务、连接；禁用时宿主会摘 http 接口、关服务器 |
| `Unload()` | 卸载 | 释放资源，之后程序集被回收 |
| `ConfigMap()` / `UpdateMap(data)` | 面板打开 / 保存设置 | 声明配置项、接收新值并落盘 |
| `Reload()` | 面板重载 | 重新读配置，就是 `Disable → Reload → Enable` 的中间那步 |

`RegisterEventHook` 里那句订阅：

```csharp
register.SubscribeMessage(message => message.Message is TextMessage text
    ? [new TextMessage("收到：" + text.Text)]
    : []);
```

- `message` 是 `ProxyMessage`，`message.Message` 是统一消息模型 `BaseMessage`
- `is TextMessage text` 是模式匹配：只有文本消息才进来，`text` 是解出来的强类型
- 返回 `List<BaseMessage>`：`[new TextMessage(...)]` 是回一条，`[]` 是不回
- 回调是同步的，要尽快返回；慢活换成 `SubscribeMessage(async e => ...)`，处理完 `await e.Reply(...)`

`RegisterEventHook` 里还能用的注册方法（按需加）：

| 方法 | 意义 |
| --- | --- |
| `RegisterPluginEvent(eventId)` | 注册自定义事件，拿到 `RaiseHandle`，随时 `Raise(...)` 广播给别的组件 |
| `RegisterState(name, label, description)` | 单值状态，面板上显示成 `名字: 值` |
| `RegisterKvState(name, label, description)` | 只读键值状态，面板上显示成可展开的表格 |
| `RegisterSend()` | 拿 `SendHandle`，主动给某个实例发消息 |
| `SubscribeSendFilter((msg, reply) => ...)` | 发送前拦截，返回 `true` 取消这条消息 |
| `RegisterHttpPath(handler)` | 注册 `/api/{组件id}` 接口 |

### 代理

最小代理（完整例子见 [proxy.md](./reference/proxy.md)）：组件负责建实例，实例负责和平台收发。

```csharp
public class ProxyMain : IBaseProxy
{
    public bool Init(string path) => true;
    public bool Load() => true;
    public bool Unload() => true;
    public bool Enable() => true;
    public bool Disable() => true;
    public void Reload() { }
    public Dictionary<string, ConfigMap> ConfigMap() => new();
    public void UpdateMap(Dictionary<string, ConfigMap> data) { }

    // 组件级订阅，一般空着
    public void RegisterEventHook(IRegister register) { }

    // 一个实例 = 一个账号 / 一条连接
    public IProxyInstasnce CreateInstance() => new DemoInstance();

    // 组件配置变了，推给实例
    public void Reload(IProxyInstasnce instasnce) { }
}

public class DemoInstance : IProxyInstasnce
{
    private ProxyEventHandle? _dispatch;

    public void Init(string path, Guid uuid) { }          // Instances/{uuid}
    public bool Load() => true;
    public void Reload() { }
    public bool Unload() => true;

    // 拿分发句柄，之后收到消息用它注入
    public void RegisterEventHook(IRegister register)
    {
        _dispatch = register.RegisterProxyEvent();
    }

    public bool Enable() => true;      // 连平台
    public bool Disable() => true;     // 断平台
    public void Tick() { }             // 每秒一次，重连 / 重试放这里

    // 平台来消息：转成 ProxyMessage 注入，返回值就是插件要回的消息
    private void OnPlatformMessage(ProxyMessage message)
    {
        var replies = _dispatch?.Dispatch(message) ?? [];

        // 按平台自己的格式把 replies 发出去
    }

    // 插件主动发的消息从这里出去
    public Task SendAsync(ProxyMessage message, BaseMessage reply) => Task.CompletedTask;

    public Dictionary<string, ConfigMap> ConfigMap() => new();
    public void UpdateMap(Dictionary<string, ConfigMap> data) { }
}
```

和插件不一样的地方：

| 成员 | 什么时候调 | 该写什么 |
| --- | --- | --- |
| 组件 `CreateInstance()` | 面板新建实例 | 返回一个 `IProxyInstasnce`，一个实例对应一个账号 / 一条连接 |
| 组件 `Reload(instance)` | 组件配置保存 | 把新配置推给已有实例 |
| 实例 `Init(path, uuid)` | 实例加载时一次 | 记住 `Instances/{uuid}`，读实例配置 |
| 实例 `RegisterEventHook(register)` | 实例 `Load` 末尾一次 | 拿 `RegisterProxyEvent()` 的句柄、注册 http 路径、上报状态 |
| 实例 `Enable()` / `Disable()` | 面板启用 / 禁用 | 连上 / 断开平台，宿主会顺手关掉它起的服务器 |
| 实例 `Tick()` | 每秒一次 | 只做重连、重试这类轻活 |
| `Dispatch(message)` | 平台收到消息 | 转成 `ProxyMessage` 后调，返回值就是要回的 `BaseMessage` 列表 |
| `SendAsync(message, reply)` | 插件主动发消息 | 把 `reply` 翻译成平台格式发出去，拦截器已经放行 |

- 代理里不写业务：收到什么就 `Dispatch` 什么，插件回什么就发什么
- `Dispatch` 是同步的，收到的消息尽快转进去，别在代理里排队
- 平台协议、签名、重连都关在代理里，插件一行都不用管

不在注册阶段也能用的：`ComponentApi`（组件 / 实例管理、起服务器）和 `CertStore`（证书），见 [API 参考](./reference/api.md)。接口定义属于进阶内容，见 [API 参考](./reference/api.md#组件接口)。运行库见[第四章](./chapters/ch04-lib.md)。

## 生命周期

```text
Lib             Init ──▶ Load ──▶ Unload
Plugin / Proxy  Init ──▶ Load ──▶ Enable ──▶ Disable ──▶ Unload
Instance        Init ──▶ Load ──▶ RegisterEventHook ──▶ Enable ──▶ Tick(1s) ──▶ Disable ──▶ Unload

Plugin / Proxy 的 RegisterEventHook 在 Load 末尾调，注册 / 订阅只能在这时
重载：Disable ──▶ Reload ──▶ Enable（实例多一步 Reload(实例)）
```

- `Init` 只调一次：插件 / 代理收到 `Configs/{组件id}`，实例收到 `Instances/{uuid}`
- 启停都返回 `bool`，`false` 算失败；禁用会停实例、摘 http 接口、关服务器
- 卸载先卸载依赖它的组件，再回收 `AssemblyLoadContext`

## 参考手册

| 文档 | 查什么 |
| --- | --- |
| [消息与插件参考](./reference/plugin.md) | 统一消息类型清单、平台独有消息、DemoPlugin 命令 |
| [代理实现参考](./reference/proxy.md) | 代理消息模型、分发链路、QQ / OneBot / GsCore 的实现细节 |
| [清单与打包参考](./reference/package.md) | `Meta.json` 字段、NuGet 细节、打包和安装 |
| [接口与 API 参考](./reference/api.md) | 组件接口表、`IRegister` 全表、`ComponentApi`、http、证书 |
| [运行与构建参考](./reference/runtime.md) | 工程结构、构建、运行目录、启停与重载、面板 HTTP 接口、已知限制 |

> 上面五份是「查」的参考手册，教程在 `chapters/` 目录里按章读。
