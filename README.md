# PlugBotSharp

基于 .NET 10 的机器人框架，插件、通信代理、运行库全部是可热插拔的组件，自带 Vue 管理面板和控制台指令。

- 组件独立 `AssemblyLoadContext`，支持卸载后删除文件
- 组件依赖（组件 / NuGet）自动补全和版本校验
- 管理面板（Vue 3 + Vite）编译进 `PlugBotSharp.Core.dll`，单文件分发
- 登录、多账户、会话管理与防爆破
- 控制台指令实时查看和调整运行状态

## 解决方案结构

| 项目 | 说明 |
| --- | --- |
| `PlugBotSharp.Core` | 宿主核心，引用 `Microsoft.AspNetCore.App`，内嵌管理面板 |
| `PlugBotSharp.Core.Lib` | 核心运行库组件（id `core`，Type `Lib`），插件通常依赖它 |
| `PlugBotSharp.QQLib` | QQ 平台模型（消息体 / 键盘 / 卡片）和 `BaseMessage` ↔ QQ 消息转换（`Type` 为 `Lib`，代理和插件共用） |
| `PlugBotSharp.QQProxy` | 官方 QQ 通信代理组件，依赖 `qqlib` |
| `PlugBotSharp.OneBotLib` | OneBot 11 消息段模型和 `BaseMessage` 转换、平台专属消息段（`Type` 为 `Lib`） |
| `PlugBotSharp.OneBotProxy` | OneBot 11 通信代理组件，依赖 `onebotlib` |
| `DemoPlugin` | 示例插件：演示消息事件和各类 QQ 消息体 |
| `PlugBotSharp.Run` | 启动入口，运行目录为程序所在目录 |
| `PlugBotSharp.Web` | Vue 3 + Vite 管理面板 |
| `PlugBotSharp.Tests` | xUnit 测试 |
| `build/MetaGen` | 从 `Meta.cs` 生成 `Meta.json` 的工具（可打包成 NuGet） |
| `build/ComponentPackage.targets` | 组件打包：生成清单并把清单里声明的 dll 打成一个 zip |

## 快速开始

```bash
dotnet build PlugBotSharp.slnx
dotnet run --project PlugBotSharp.Run
```

首次启动会创建运行目录，然后：

1. 浏览器打开 <http://127.0.0.1:10000>
2. 创建初始管理员账户（**只能创建这一个**，之后的账户在面板里加）
3. 在「组件」页安装组件 zip，在「实例」页创建实例

运行目录默认是程序所在目录（`AppContext.BaseDirectory`）。`Config.json` 只有端口一项：

```json
{ "Port": 10000 }
```

面板是必须启动的，`Port` 为 0 或端口被占用会直接退出进程。

## 运行时目录

```
Config.json                    面板端口
Components/
  {组件id}/
    Meta.json                  组件清单
    *.dll
    runtimes/...               可选，native 库
Instances/
  {实例uuid}/
    Instance.json              实例定义
    config.json               实例配置，由组件自己读写
Configs/
  Accounts.json                面板账户（PBKDF2 哈希）
  Sessions.json                登录会话（只存 token 的 SHA-256）
  ComponentState.json          组件启用状态
  InstanceState.json           实例启用状态
  {组件id}/                    组件配置
Nugets/
  {包id}/{版本}/               自动补全的 NuGet 包
Logs/
  yyyy-MM-dd.log              文件日志
```

## 组件

组件目录里只需要一个 `Meta.json`（旧的 `Plugin.json` / `Proxy.json` 已不再识别）：

```json
{
  "Name": "QQProxy",
  "Id": "qq",
  "Version": "1.0.0",
  "Description": "官Q机器人",
  "Type": "Proxy",
  "Dlls": ["PlugBotSharp.QQProxy.dll"],
  "Deps": [
    { "Type": "Component", "Id": "core", "Version": ">=1.0.0" },
    { "Type": "Nuget", "Id": "SkiaSharp", "Version": "4.157.0" }
  ]
}
```

- `Type`：`Plugin` / `Proxy` / `Lib`
- `Dlls`：真正属于这个组件的程序集，会被打在安装包里
- `Deps`：`Component` 是组件依赖，`Nuget` 会自动下载
- 版本表达式支持 `>=1.0.0`、`>1.0.0`、`<=1.0.0`、`<1.0.0`、`=1.0.0` / `==1.0.0`（等于）、逗号组合；裸版本号（`1.0.0`）按最低版本处理，等价于 `>=1.0.0`

### 打包

组件工程引用 `build/ComponentPackage.targets` 并写一个 `Meta.cs`：

```xml
<ItemGroup>
  <ProjectReference Include="..\build\MetaGen\MetaGen.csproj" ReferenceOutputAssembly="false" />
</ItemGroup>
<Import Project="..\build\ComponentPackage.targets" />
```

构建后自动生成 `Meta.json`，并把清单声明的 dll 打成 `{AssemblyName}.zip`，直接拖进面板安装即可。

### 组件接口

组件能力拆在几个接口里，宿主只依赖接口：

| 接口 | 方法 |
| --- | --- |
| `IBaseComponent` | `Load` `Reload` `Unload` `Enable` `Disable` `ConfigMap` `UpdateMap` |
| `IComponent : IBaseComponent` | `Init(string path)` |
| `IBasePlugin : IComponent` | `RegisterEventHook()` |
| `IBaseProxy : IComponent` | `CreateInstance()` `Reload(IProxyInstasnce)` |
| `IProxyInstasnce : IBaseComponent` | `Init(path, uuid)` `RegisterEventHook()` `Tick()` |
| `IBaseLib` | `Init` `Load` `Unload`（运行库不参与启停） |

- 启停方法都是同步的 `bool`，`Reload`、`UpdateMap`、`RegisterEventHook` 返回 `void`
- `ConfigMap` 返回当前配置项，`UpdateMap` 收面板提交的同一批配置项，插件和代理都有
- `RegisterEventHook` 在 `Load` 之后、`Enable` 之前调用一次，抛异常只记日志，不影响加载

### 生命周期

| 类型 | 生命周期 |
| --- | --- |
| 运行库 `Lib` | `Init → Load → Unload`（不调用 Enable / Disable） |
| 插件 | `Init → Load → RegisterEventHook → Enable → Disable → Unload` |
| 代理 | `Init → Load → Enable → Disable → Unload` |
| 实例 | `Init → Load → RegisterEventHook → Enable → Tick(1s) → Disable` |
| 组件重载配置 | `Disable` → `Reload` → `Enable` |
| 实例重载配置 | `Disable实例` → `Reload(IProxyInstasnce instasnce)` → `Reload实例` → `Enable实例` |

- `Init` 只调用一次，参数是组件配置目录 `Configs/{组件id}`
- 实例的 `Init(path, uuid)` 收到的是实例目录 `Instances/{uuid}`，配置文件放这里
- 禁用组件会自动移除它注册的 http 接口
- 卸载会先卸载依赖它的组件，再卸载整个 `AssemblyLoadContext`，并手动 `Free` 组件加载的 native 库；卸载完成后组件目录可以删除
- 卸载是运行时行为，重启后仍会重新加载；要彻底移除请用「删除」
- 重载对插件和代理都有效：本来就未启用时只调用 `Reload`，不做多余的 Disable / Enable；重载实例同理，未启用时只重读配置，不会把它启用起来
- 面板里保存设置后会自动调用对应的重载接口，也可以单独点「重载」

### 事件

`EventManager` 分三路，插件在 `RegisterEventHook()` 里注册（实例也有这个方法），组件卸载时会自动摘掉它的全部注册：

| 事件 | 注册 | 触发 |
| --- | --- | --- |
| 消息事件 | `RegisterMessageHook(owner, Func<ProxyMessage, List<BaseMessage>>)` | 代理收到消息后调用 `HandleMessage(...)`，返回值就是要回复的消息 |
| Proxy 事件 | `RegisterProxyHook(owner, Action<ProxyEvent>)` | 实例启用 / 停用时广播 `Started` / `Stopped` |
| 插件事件 | `RegisterPluginHook(owner, Action<PluginEvent>)` | 插件 `Load` / `Enable` / `Disable` / `Unload` 时广播；`RaisePlugin(id, name, data)` 是插件之间自定义的事件 |

```csharp
public void RegisterEventHook()
{
    EventManager.RegisterMessageHook(this, msg => msg.Message is TextMessage text
        ? [new TextMessage("收到：" + text.Text)]
        : []);

    EventManager.RegisterPluginHook(this, e =>
    {
        if (e.Type == PluginEventType.Custom && e.Name == "ping")
        {
            // 处理其他插件发来的事件
        }
    });
}
```

- 多个插件都返回消息时按注册顺序拼起来，由调用 `HandleMessage` 的代理负责发送
- 某个回调抛异常只记日志，不影响其他回调
- `ProxyMessage` 带 `Id`（代理 id）、`Uuid`（实例）、`SenderType` / `Sender` / `Target`（回复目标）和消息内容，被动回复要用的 `ReplyId` / `ReplyEventId` 也在这里

### 通用消息

各平台都有的消息已经是 `BaseMessage` 的子类型，代理各自映射到自己的格式：

| 类型 | QQ | OneBot 11 | KOOK |
| --- | --- | --- | --- |
| `TextMessage` | `msg_type: 0` | `text` 段 | `type: 1` |
| `ImageUrlMessage` / `ImageFileMessage` | 先上传换 `file_info` | `image` 段 | `type: 2` |
| `MentionMessage` | 目前走直通 | `at` 段 | KMarkdown `(met)` |
| `FaceMessage` | 目前走直通 | `face` / `mface` 段 | KMarkdown `(emj)` |
| `RecordMessage` | 需要上传 | `record` 段 | `type: 8` |
| `VideoMessage` | 需要上传 | `video` 段 | `type: 3` |
| `CardMessage` | 目前走直通 | `json` / `xml` 段 | `type: 10` |
| `MarkdownMessage` | `msg_type: 2` | `markdown` 段 | `type: 9` |
| `ReplyMessage` | `message_reference` | `reply` 段 | 引用回复 |

### 平台独有消息

要发平台独有的结构（QQ 的内嵌键盘、图文卡片、ark），用 `RawMessage` 把 payload 原样交给代理：

```csharp
// 插件里，引用 qqlib 是为了少写 JSON
var obj = new SendGroupMessageObj
{
    MsgType = 2,
    Markdown = new MessageMarkdownObj { Content = "**hi**" }
};

return [new RawMessage(QQMessage.SendName, JsonSerializer.SerializeToNode(obj, QQJsonContext.Default.SendGroupMessageObj)!)];
```

- `RawMessage.Name` 是约定，QQ 代理认 `qq.send`，转不过来的直接丢弃
- payload 用 `JsonNode` 而不是强类型对象：万一某个组件把 `QQLib.dll` 私自打进了自己的包，也只是多一次序列化，不会因为类型标识不同炸掉
- 接收方向仍然是 `BaseMessage`，插件不需要懂平台字段

`DemoPlugin` 里每个命令对应一种消息体，可以直接当模板抄：

| 命令 | 发出的消息 |
| --- | --- |
| `/text` | `TextMessage`，内容可以在面板的设置里改 |
| `/markdown` | `MarkdownMessage`，QQ 侧转成 `msg_type: 2`，OneBot 侧转成 `markdown` 段 |
| `/keyboard` | markdown + 内嵌键盘（指令按钮 + 跳转按钮） |
| `/card` | `msg_type: 3` 图文卡片 |
| `/list` | `ListMessage`，代理会拆成多条发送 |
| `/reply` | `ListMessage(ReplyMessage, TextMessage)`，引用收到的消息回复 |

**依赖 qqlib 的组件只写 `Deps`，不要把 `PlugBotSharp.QQLib.dll` 放进自己的 `Dlls`。** 组件的加载上下文会先找自己目录里的 dll，打进包就等于每个组件各加载一份，类型标识不再相同。

### QQ 代理

- 收到 `GROUP_AT_MESSAGE_CREATE` / `GROUP_MESSAGE_CREATE`（群）和 `C2C_MESSAGE_CREATE`（单聊）时转成消息事件，其他事件只记日志
- 群消息的 `ProxyMessage`：`SenderType = Group`、`Sender = member_openid`、`Target = group_openid`、`ReplyId = 平台消息 id`；单聊的 `Target` 为空，回复时用 `Sender`
- 内容里的 `<@!id>` / `<qqbot-at-user .../>` 会被去掉，附件按 `content_type` 转成 `ImageUrlMessage` 或 `FileUrlMessage`，有文字时合成 `ListMessage`
- 回复走被动回复：带 `msg_id`（`ReplyId`）和自增的 `msg_seq`，群里发到 `v2/groups/{group_openid}/messages`，单聊发到 `v2/users/{openid}/messages`
- `HookType` 三种模式：`WebHook` 注册到宿主（`/api/qq/{实例uuid}`）；`WebHost` 自己起 Kestrel，监听实例配置里的 `Port`；`WebSocket` 还没实现

### OneBot 代理

- 反向 WebSocket（我们连到 OneBot 实现，例如 NapCat 的 `ws://127.0.0.1:3001`），实例配置 `Url` / `Token` / `ReconnectSeconds`
- 断线由 `Tick` 按 `ReconnectSeconds` 重连，连接状态变化会写日志
- 收到 `post_type: message` 的群消息 / 私聊消息转成 `ProxyMessage`，`Target` 是群号，私聊为空，`ReplyId` 是 `message_id`
- 消息段按 `text` / `at` / `image` / `face` / `mface` / `record` / `video` / `json` / `xml` / `markdown` / `reply` 转成通用消息，多段合成 `ListMessage`
- 发送走 `send_group_msg` / `send_private_msg`，内容是一组消息段；被动回复会自动在头部带 `reply` 段
- OneBot 专属的段（音乐、位置、名片、戳一戳、骰子、猜拳、抖动、合并转发、分享）用 `OneBotSegment` 拼，再包成直通消息：

```csharp
return [OneBotSegment.Raw(OneBotSegment.Music("qq", "123456"))];
```

### 组件里注册 http 接口

回调是 `Func<HttpContext, Task>`，宿主会 `await` 它：

```csharp
ComponentApi.RegisterHttpPath(plugin, HandleAsync);    // /api/{组件id}
ComponentApi.RegisterHttpPath(proxy, HandleAsync);     // /api/{组件id}
ComponentApi.RegisterHttpPath(instance, HandleAsync);  // /api/{组件id}/{实例uuid}

static async Task HandleAsync(HttpContext ctx)
{
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsync("""{"ok":true}""");
}

ComponentApi.UnregisterHttpPath(plugin);
ComponentApi.UnregisterHttpPath(proxy);
ComponentApi.UnregisterHttpPath(instance);
```

- 路径由组件 id（实例再加 uuid）决定，同一个路径重复注册会覆盖
- 回调里的异常由宿主捕获，记日志并返回 500（响应已经开始时只记日志）
- `UnregisterHttpPath` 返回是否真的移除了注册；只有注册者一致才移除，重复调用返回 `false`，组件已经卸载也能调用
- 禁用或卸载时对应接口会自动移除，手动取消注册用来提前关掉接口

### 组件和实例的设置

组件（`IBaseComponent`，插件和代理都行）和实例（`IProxyInstasnce`）都可以把配置项声明出来，面板照着生成表单，保存时再回调组件：

```csharp
public Dictionary<string, ConfigMap> ConfigMap() => new()
{
    ["Port"] = new ConfigIntMap { Key = "Port", Value = 10000, Min = 1, Max = 65535, Default = 10000 },
    ["AppSecret"] = new ConfigStringMap { Key = "AppSecret", Value = "", Secret = true },
};

public void UpdateMap(Dictionary<string, ConfigMap> data)
{
    // data 里是面板提交上来的同一批配置项，取 Value 自己落盘
}
```

| `type` | 记录 | 面板控件 |
| --- | --- | --- |
| `int` | `ConfigIntMap` | 数字框，带 `Min` / `Max` |
| `float` | `ConfigFloatMap` | 小数框，带 `Min` / `Max` |
| `bool` | `ConfigBoolMap` | 开关 |
| `string` | `ConfigStringMap` | 文本框，`Secret` 时是密码框，`MaxLength` 限长 |
| `list` | `ConfigListMap` | 多行文本，一行一项，`MaxCount` 限项数 |
| `kv` | `ConfigKvMap` | 键值对，可增删行 |
| `enum` | `ConfigEnumMap` | 下拉框，选项来自 `Options` |

- `Key` 是配置名，也是面板上显示的字段名；`Value` 是当前值，`Default` 用来点「默认值」
- 面板接口是 `GET/POST /api/admin/components/{id}/config` 和 `GET/POST /api/admin/instances/{uuid}/config`
- 保存后可以调 `POST .../reload` 让配置立刻生效，面板会自动调
- 序列化用独立的 `ConfigMapContext`，字段是 camelCase，多态判别字段是 `type`

## 管理面板

| 页签 | 功能 |
| --- | --- |
| 概览 | 运行时长、组件 / 实例数量、核心版本 |
| 组件 | 组件列表、zip 安装、加载、卸载、启用、禁用、重载、删除、按类型筛选、设置 |
| 实例 | 创建实例、加载、启用、禁用、重载、删除、设置 |
| 账户 | 添加账户、修改 / 重置密码、删除账户、查看和撤销登录会话 |

### 账户与安全

- 密码使用 PBKDF2-SHA256（12 万次迭代 + 随机盐），校验用固定时间比较
- 登录 cookie `pbs.sid`：`HttpOnly` + `SameSite=Strict`，7 天有效，访问时滑动续期，最长 30 天
- 服务端只保存 token 的 SHA-256，`Sessions.json` 泄露也拿不到可用 token
- 防爆破：同一账户 10 分钟内错 5 次锁 10 分钟；同一 IP 错 20 次锁 30 分钟；失败固定延迟 400ms
- 初始账户只能创建一次，之后只能登录后添加
- 至少要保留一个账户，不能删除当前登录的账户
- 面板只监听 `127.0.0.1`，不要直接暴露到公网

## 控制台指令

进程运行时在控制台输入：

| 指令 | 说明 |
| --- | --- |
| `help` / `?` | 帮助 |
| `status` / `st` | 运行状态：版本、时长、面板地址、组件和实例数量 |
| `components [type]` | 组件列表，`type` 可选 `plugin` / `proxy` / `lib` |
| `instances` / `inst` | 实例列表 |
| `load <id>` / `unload <id>` | 加载 / 卸载组件 |
| `enable <id>` / `disable <id>` | 启用 / 禁用组件 |
| `clear` / `cls` | 清屏 |
| `stop` / `exit` / `quit` | 停止并退出 |

日志同时输出到控制台和 `Logs`。以服务方式运行、没有标准输入时，控制台线程会自动退出，不影响进程运行。

## HTTP 接口

| 路径 | 说明 |
| --- | --- |
| `GET /api/auth/state` | 是否已初始化、是否已登录（公开） |
| `POST /api/auth/init` | 创建初始账户，只能成功一次（公开） |
| `POST /api/auth/login` / `logout` | 登录 / 退出 |
| `GET /api/status` | 基础状态（公开） |
| `/api/admin/**` | 面板接口，需要登录 |
| `GET/POST /api/admin/components/{id}/config` | 组件设置项，读 `ConfigMap`、写 `UpdateMap` |
| `POST /api/admin/components/{id}/reload` | 重载组件配置 |
| `GET/POST /api/admin/instances/{uuid}/config` | 实例设置项，同上 |
| `POST /api/admin/instances/{uuid}/reload` | 重载实例配置 |
| `/api/{组件id}` | 组件自己注册的接口（公开） |

响应统一为 `{ "ok": bool, "message": string, "data": any }`。

## 开发

```bash
dotnet build PlugBotSharp.slnx
dotnet test PlugBotSharp.Tests
```

前端：

```bash
cd PlugBotSharp.Web
npm install
npm run dev     # http://127.0.0.1:5173，/api 代理到 10000
npm run build   # 产物在 dist，编译 Core 时会自动嵌入 dll
```

编译 Core 默认会执行前端构建，`-p:BuildAdminPanel=false` 可以跳过。

## 已知限制

- NuGet 只解析组件直接声明的包，不解析传递依赖；需要 native 库时请在 `Deps` 里显式列出
- 卸载组件时手动 `Free` native 库，前提是组件自己的线程已经停止
- 登录防爆破状态是内存态，进程重启后计数清零
