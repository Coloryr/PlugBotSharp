# PlugBotSharp 开发文档

写给要**写组件**和**改宿主**的人。装组件、用面板、看协议细节见 [根目录 README](../README.md)。

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

最小插件（完整例子见 [plugin.md](plugin.md)）：

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

    public void RegisterEventHook(IRegister register)
    {
        // 收到消息就回一句
        register.SubscribeMessage(message => message.Message is TextMessage text
            ? [new TextMessage("收到：" + text.Text)]
            : []);
    }
}
```

最小代理（完整例子见 [proxy.md](proxy.md)）：继承 `IBaseProxy`，`CreateInstance()` 返回一个 `IProxyInstasnce`；实例在 `RegisterEventHook` 里拿到 `RegisterProxyEvent()` 的句柄，收到平台消息时调 `Dispatch(message)`，返回值就是要回的消息。

接口定义（`IBaseComponent` / `IBasePlugin` / `IBaseProxy` / `IProxyInstasnce` / `IBaseLib`）属于进阶内容，见 [api.md](api.md#组件接口)。

## 生命周期

```text
运行库 Lib
    Init
     ↓
    Load
     ↓
    Unload


插件 Plugin / 代理 Proxy
    Init
     ↓
    Load ──▶ 末尾调用 RegisterEventHook（注册 / 订阅只能在这里）
     ↓
    Enable ──▶ 运行中
     ↓
    Disable
     ↓
    Unload


代理实例 Instance
    Init
     ↓
    Load
     ↓
    RegisterEventHook
     ↓
    Enable ──▶ 运行中，每秒 Tick 一次（重连、重试放这里）
     ↓
    Disable
     ↓
    Unload


重载配置
    组件  Disable ──▶ Reload ──▶ Enable        本来没启用就只调 Reload
    实例  Disable 实例 ──▶ Reload(实例) ──▶ Reload 实例 ──▶ Enable 实例
```

- `Init` 只调用一次：插件 / 代理收到 `Configs/{组件id}`，实例收到 `Instances/{uuid}`，配置文件放这里
- `RegisterEventHook` 在 `Load` 之后、`Enable` 之前调用一次，注册器只在这时可用，抛异常只记日志、不影响加载
- 启停方法都是同步的 `bool`，返回 `false` 算启用 / 加载失败，不会让进程崩掉
- 禁用会停掉实例、摘掉这个组件注册的 http 接口、关掉它起的服务器；卸载会先卸载依赖它的组件，再回收 `AssemblyLoadContext`
- 卸载 / 禁用都是运行时行为，重启后会重新加载；要彻底移除用面板的「删除」
- `Tick` 每秒一次，只做重连、重试这类轻活，别阻塞

## 组件能用的 API

注册阶段拿到 `IRegister`，能做的事：

| 能力 | API | 用在哪 |
| --- | --- | --- |
| 自定义事件 | `RegisterPluginEvent(eventId)` → `RaiseHandle` | 插件、代理、实例都能发，别的组件按 `[组件id]事件id` 订阅 |
| 收消息 | `SubscribeMessage(Func<ProxyMessage, List<BaseMessage>>)` / `SubscribeMessage(Func<MessageEvent, Task>)` | 插件处理消息，代理转发消息 |
| 发消息 / 拦截 | `RegisterSend()` → `SendHandle.SendAsync`、`SubscribeSendFilter(...)` | 主动发消息、屏蔽某条消息 |
| 注入消息 | `RegisterProxyEvent()` → `ProxyEventHandle.Dispatch` | **只有代理 / 实例**能拿 |
| 状态 | `RegisterState(name, label, description)` / `RegisterKvState(...)` | 面板上显示运行时数据 |
| http 接口 | `RegisterHttpPath(Func<HttpContext, Task>)` | `/api/{组件id}`，实例是 `/api/{组件id}/{uuid}` |

不在注册阶段也能用的：

| 能力 | API |
| --- | --- |
| 配置项 | `ConfigMap()` 声明、`UpdateMap()` 接收，面板生成表单 |
| 宿主管理 | `ComponentApi.GetComponents()` / `GetInstances()` / `StartComponent` / `EnableInstance` … |
| 自建服务器 | `ComponentApi.StartServer(owner, ServerOptions, configure)`，组件结束时自动关闭 |
| 证书 | `CertStore.Names()` / `CertStore.List()` |

```csharp
public void RegisterEventHook(IRegister register)
{
    register.SubscribeMessage(msg => ...);                    // 收消息
    _ping = register.RegisterPluginEvent("ping");               // 自定义事件
    _replies = register.RegisterState("replies", "回复条数", "累计回复条数");
    register.RegisterHttpPath(HandleAsync);                     // /api/{组件id}
    _send = register.RegisterSend();                            // 主动发消息
}
```

细节、参数和注意事项都在 [api.md](api.md)：事件与句柄、状态、配置、`ComponentApi`、http 和证书。

## 文档索引

| 文档 | 内容 |
| --- | --- |
| [plugin.md](plugin.md) | 写插件：生命周期、订阅消息、通用消息、平台消息、DemoPlugin |
| [proxy.md](proxy.md) | 写代理：代理和实例、`ProxyMessage`、分发与回复、ProxyDemo、现有代理说明 |
| [lib.md](lib.md) | 写运行库：DemoLib、QQLib / OneBotLib |
| [package.md](package.md) | 组件清单 `Meta.json`、依赖、NuGet、打包和安装 |
| [api.md](api.md) | 进阶：组件接口、注册器和句柄、事件、状态、配置、ComponentApi、http 接口、证书 |
| [runtime.md](runtime.md) | 工程结构、构建、运行目录、启停与重载、面板 HTTP 接口、日志、已知限制 |
