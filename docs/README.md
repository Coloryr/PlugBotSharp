# PlugBotSharp 开发文档

面向二次开发和写组件的人。使用说明见 [根目录 README](../README.md)。

## 解决方案结构

| 目录 | 项目 | 说明 |
| --- | --- | --- |
| `Core/` | `PlugBotSharp.Core` | 宿主核心，引用 `Microsoft.AspNetCore.App`，内嵌管理面板 |
| `Lib/` | `PlugBotSharp.Core.Lib` | 核心运行库组件（id `core`，Type `Lib`），插件通常依赖它 |
| `Lib/` | `PlugBotSharp.QQLib` | QQ 平台模型（消息体 / 键盘 / 卡片）和 `BaseMessage` ↔ QQ 消息转换（Type `Lib`，代理和插件共用） |
| `Lib/` | `PlugBotSharp.OneBotLib` | OneBot 11 消息段模型和 `BaseMessage` 转换、平台专属消息段（Type `Lib`） |
| `Plugin/` | `DemoPlugin` | 示例插件：演示消息事件和各类消息体 |
| `Proxy/` | `PlugBotSharp.QQProxy` | 官方 QQ 通信代理组件，依赖 `qqlib` |
| `Proxy/` | `PlugBotSharp.OneBotProxy` | OneBot 11 / 12 通信代理组件，依赖 `onebotlib` |
| `Proxy/` | `PlugBotSharp.GenshinProxy` | gsuid_core 通信代理组件：把平台消息单向推给 core |
| 根目录 | `PlugBotSharp.Run` | 启动入口，运行目录为程序所在目录 |
| 根目录 | `PlugBotSharp.Web` | Vue 3 + Vite 管理面板 |
| 根目录 | `PlugBotSharp.Tests` | xUnit 测试 |
| `build/` | `MetaGen` | 从 `Meta.cs` 生成 `Meta.json` 的工具（可打包成 NuGet） |
| `build/` | `ComponentPackage.targets` | 组件打包：生成清单并把清单里声明的 dll 打成一个 zip |

## 组件清单

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

### 组件之间的依赖

- 依赖别的组件只写 `Deps`，**不要把对方的 dll 放进自己的 `Dlls`**
- 加载上下文会先找自己目录里的 dll，打进包就等于每个组件各加载一份，类型标识不再相同
- 组件依赖会按拓扑序加载，被依赖的组件先加载；卸载时会先卸载依赖它的组件

## 组件接口

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

## 生命周期

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

## 事件

事件注册统一走 `ComponentApi`，而且只能在 `RegisterEventHook()` 里注册（插件和实例都有这个方法），组件卸载时宿主会自动摘掉它的全部注册。`EventManager` 是宿主内部类，组件拿不到。

| 事件 | 注册 | 触发 |
| --- | --- | --- |
| 消息事件 | `ComponentApi.RegisterMessageHook(owner, Func<ProxyMessage, List<BaseMessage>>)` | 代理收到消息后调用 `ComponentApi.DispatchMessage(msg)`，返回值就是要回复的消息 |
| Proxy 事件 | `ComponentApi.RegisterProxyHook(owner, Action<ProxyEvent>)` | 实例启用 / 停用时广播 `Started` / `Stopped` |
| 插件事件 | `ComponentApi.RegisterPluginHook(owner, Action<PluginEvent>)` | 插件 `Load` / `Enable` / `Disable` / `Unload` 时广播；`ComponentApi.RaisePlugin(id, name, data)` 是插件之间自定义的事件 |
| 发送钩子 | `ComponentApi.RegisterSendHook(owner, Func<ProxyMessage, BaseMessage, Task>)` | 组件之间转发消息用：`ComponentApi.SendAsync(context, message)` 交给所有实例，目标实例按 `context.Uuid` 自己过滤 |

```csharp
public void RegisterEventHook()
{
    ComponentApi.RegisterMessageHook(this, msg => msg.Message is TextMessage text
        ? [new TextMessage("收到：" + text.Text)]
        : []);

    ComponentApi.RegisterPluginHook(this, e =>
    {
        if (e.Type == PluginEventType.Custom && e.Name == "ping")
        {
            // 处理其他插件发来的事件
        }
    });
}
```

- 多个插件都返回消息时按注册顺序拼起来，由调用 `DispatchMessage` 的代理负责发送
- 某个回调抛异常只记日志，不影响其他回调
- `ProxyMessage` 带 `Id`（代理 id）、`Uuid`（实例）、`SenderType` / `Sender` / `Target`（回复目标）和消息内容，被动回复要用的 `ReplyId` / `ReplyEventId` 也在这里
- 实例要收别的代理的消息，也是在 `RegisterEventHook()` 里注册消息事件，回调里用自己的传输发

## 通用消息

各平台都有的消息是 `BaseMessage` 的子类型，代理各自映射到自己的格式：

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

每个消息类型一个文件，在 `Core/PlugBotSharp.Core/Messages` 下；`ListMessage` 用来把多条合成一条发送，代理会按自己的限制拆开。

## 平台独有消息

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

- `RawMessage.Name` 是约定，QQ 代理认 `qq.send`，OneBot 代理认 `onebot11.segment`，转不过来的直接丢弃
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

## QQ 代理

- 收到 `GROUP_AT_MESSAGE_CREATE` / `GROUP_MESSAGE_CREATE`（群）和 `C2C_MESSAGE_CREATE`（单聊）时转成消息事件，其他事件只记日志
- 群消息的 `ProxyMessage`：`SenderType = Group`、`Sender = member_openid`、`Target = group_openid`、`ReplyId = 平台消息 id`；单聊的 `Target` 为空，回复时用 `Sender`
- 内容里的 `<@!id>` / `<qqbot-at-user .../>` 会被去掉，附件按 `content_type` 转成 `ImageUrlMessage` 或 `FileUrlMessage`，有文字时合成 `ListMessage`
- 回复走被动回复：带 `msg_id`（`ReplyId`）和按消息 id 自增的 `msg_seq`，群里发到 `v2/groups/{group_openid}/messages`，单聊发到 `v2/users/{openid}/messages`
- `HookType` 三种模式：`WebHook` 注册到宿主（`/api/qq/{实例uuid}`）；`WebHost` 自己起 Kestrel，监听实例配置里的 `Port`；`WebSocket` 还没实现

## OneBot 代理

实例配置拆成四个开关：`Transport`（`WebSocket` / `Http`）、`Role`（`Client` / `Server`）、`Receive`（收不收事件）、`Send`（发不发消息）。

| Transport | Role | 收事件 | 发消息 |
| --- | --- | --- | --- |
| `WebSocket` | `Client` | 我们连 `Url`（例如 `ws://127.0.0.1:3001`）的 socket | 同一条 socket |
| `WebSocket` | `Server` | 机器人连我们的 `Host:Port` 的 `/ws` | `Url` 上的 HTTP API |
| `Http` | `Server` | 机器人 POST 到 `Host:Port` 的 `/` | `Url` 上的 HTTP API |
| `Http` | `Client` | 收不到（正向 HTTP 只有接口，没有事件） | `Url` 上的 HTTP API |

- `Receive` / `Send` 再各管一半：关掉 `Send` 就是只收，关掉 `Receive` 就是只发；两个都关或者服务端模式关掉 `Receive`，`Enable` 直接失败
- 服务端模式（`Role = Server`）用 `Token` 校验 `Authorization: Bearer`；客户端 WebSocket 模式断线由 `Tick` 按 `ReconnectSeconds` 重连
- OneBot 专属的段（音乐、位置、名片、戳一戳、骰子、猜拳、抖动、合并转发、分享）用 `OneBotSegment` 拼，再包成直通消息：

```csharp
return [OneBotSegment.Raw(OneBotSegment.Music("qq", "123456"))];
```

### 协议版本

实例配置里的 `Protocol` 选 `V11` / `V12`，两版的段结构一样，只有事件字段和 action 不同：

| | V11 | V12 |
| --- | --- | --- |
| 事件类型 | `post_type` + `message_type` | `type` + `detail_type` |
| 消息体 | `message` 段数组或 CQ 字符串 | `message` 段数组 |
| ID | 数字（`user_id` / `group_id`） | 字符串 |
| 发送 action | `send_group_msg` / `send_private_msg` | `send_message` + `detail_type` |
| @ 段 | `at`（`qq=all` 是全体） | `mention` / `mention_all` |
| 事件标记 | 带 `post_type` 就算事件 | 带 `type` 和 `self` 才算事件 |

- 接收：`OneBotEvent.Parse(..., version)` 按版本解析，两边都转成同一个 `ProxyMessage`
- 发送：`OneBotAction.SendMessage(version, group, target, segments)` 组 action 和参数，v12 的 id 原样传字符串
- 消息段：`OneBotMessage.ToSegments(message, version)` / `FromSegments(json, out replyId, version)`，`MentionMessage` 在 v12 会转成 `mention` / `mention_all`

## 起 http / websocket 服务器

组件要自己监听端口时不要 new `WebApplication`，交给宿主托管，组件或实例结束时宿主会自动关掉：

```csharp
private ComponentServer? _server;

public bool Start(ushort port)
{
    _server ??= ComponentApi.StartServer(this, new ServerOptions
    {
        Host = "127.0.0.1",
        Port = port,
        Certificate = "panel",      // Certs 里的证书名，也能填 pfx 路径，为空走 http
        WebSocket = true            // 需要 WebSocket 时置为 true，宿主会 UseWebSockets()
    }, app =>
    {
        app.MapPost("/", HandlePost);
        app.Map("/ws", HandleSocket);
    });

    return _server.Start();
}
```

- `StartServer` 把服务器登记在 `owner`（插件 / 代理 / 实例对象）名下，宿主负责建 Kestrel、解析证书（同样走 `CertStore`）
- 组件禁用 / 卸载、实例禁用 / 删除时，宿主调用 `ServerRegistry.StopAll(owner)` 关掉它名下的全部服务器，组件不用自己收尾
- `ComponentServer.Start()` 同步执行，成功返回 `true`；`Stop()` 可以重复调用
- QQ 代理的 `WebHost` 模式、OneBot 代理的反向服务端都改成了这套，组件里不再出现 `WebApplication`

### 选证书

`ServerOptions.Certificate` 填 `Certs` 目录里的证书名即可，密码自动按 `Certs.json `或密码文件（`panel.txt` / `password.txt` 这类）解析：

```csharp
new ServerOptions
{
    Host = "127.0.0.1",
    Port = 3002,
    Certificate = "panel",   // Certs 里的证书名，密码自动找
    WebSocket = true
}
```

- 名字找不到时会把 `Certificate` 当成本地 pfx 路径，这时密码用同一条配置里的 `CertificatePassword`
- 名字或路径都解析不到时 `Start()` 返回 `false` 并记一条「找不到证书」日志，不会静默退回 http
- 证书本身要带 Server Authentication 用途（IIS 导出的证书默认就带），否则 Kestrel 会拒绝启动
- 想知道有哪些证书可用：`CertStore.Names()` 拿名字列表，`CertStore.List()` 拿带路径和密码的完整信息

## Genshin 代理

`gsuid_core`（SayuCore）把原神功能跑在独立进程里，这个代理只负责**把平台消息单向推给 core**，不处理 core 下发的消息：

```
QQ / OneBot 实例 ──消息事件──▶ Genshin 代理 ──MessageReceive──▶ gsuid_core
```

- 连的是 `{Url}/ws/{BotId}`，有 `Token` 就带上 `?token=`；断线由 `Tick` 按 `ReconnectSeconds` 重连
- 在 `RegisterEventHook()` 里订阅所有平台的消息事件，转成 `MessageReceive`（文本、@、图片、语音、视频、markdown、引用），按 `group/direct` 填 `user_type`
- 消息段：`text` / `at` / `image` / `file` / `record` / `video` / `markdown` / `reply_id`，媒体用 `link://` 或 `base64://`，平台私有段用 `RawMessage("genshin.segment")` 直通
- 只发不收：收到 core 的报文会读掉但不做处理，避免缓冲区堆积

| 配置 | 说明 |
| --- | --- |
| `Url` | gsuid_core 地址，例如 `ws://127.0.0.1:8765` |
| `BotId` | 连接用的 bot_id，core 靠它区分适配器 |
| `Token` | core 配置的 ws token，密码框 |
| `ReconnectSeconds` | 断线重连间隔 |

组件级还有 `LogMessage`，打开后把发出去的报文写进调试日志。

## 组件里注册 http 接口

## 组件里注册 http 接口

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

## 组件和实例的设置

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
- `Description` 是给用户看的解释，面板上显示在输入框上方；`Placeholder` 是输入框的占位符
- 面板接口是 `GET/POST /api/admin/components/{id}/config` 和 `GET/POST /api/admin/instances/{uuid}/config`
- 保存后可以调 `POST .../reload` 让配置立刻生效，面板会自动调
- 序列化用独立的 `ConfigMapContext`，字段是 camelCase，多态判别字段是 `type`

## 证书 / HTTPS

所有自己起的 http / websocket 服务器共用宿主的 `Certs` 目录，密码不用手填：

```
Certs/
  panel.pfx
  panel.txt                    密码文件，一行密码
  cert.zip                     也可以直接丢压缩包
  Certs.json                   可选，用来起别名或者固定密码
```

- **压缩包**：`Certs` 下的 `.zip` 会自动解到同名子目录，里面一般是 pfx + 密码文件
- **密码自动识别**：先找同目录的 `<pfx名>.txt` / `<pfx名>.password`，再找 `password.txt` / `pwd.txt` / `密码.txt` / `cert.txt`，取第一行非空内容
- **清单**：`Certs.json` 可以给证书起别名、固定密码；密码留空就按上面的规则自动找
- **选择证书**：实例设置里是下拉框，选项来自 `Certs` 目录，空表示不用；也兼容直接填 pfx 路径

```json
{
  "Items": [
    { "Name": "panel", "File": "panel.pfx" }
  ]
}
```

| 位置 | 配置项 | 说明 |
| --- | --- | --- |
| 管理面板 | `Config.json` 的 `Certificate` | 面板地址变成 `https://127.0.0.1:{Port}` |
| QQ 代理 | 实例配置的 `Certificate` | 只对 `HookType = WebHost` 生效 |
| OneBot 代理 | 实例配置的 `Certificate` | 只对 `Role = Server` 生效，WebSocket 一起变成 wss |

证书名找不到或者文件不存在，服务器启动失败并记日志，不会静默退回 http。

面板接口 `GET /api/admin/certs` 只返回名字和文件名，不会返回密码。

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

测试里不允许依赖真实程序集，组件用 `PlugBotSharp.Tests` 里的假实现；测试之间会清空事件注册和组件字典，不要假设顺序。

## 已知限制

- NuGet 只解析组件直接声明的包，不解析传递依赖；需要 native 库时请在 `Deps` 里显式列出
- 卸载组件时手动 `Free` native 库，前提是组件自己的线程已经停止
- 登录防爆破状态是内存态，进程重启后计数清零
- OneBot 反向 WS 模式目前只能收事件，发送仍走 HTTP API