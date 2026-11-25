# 组件接口和 API

组件能用的宿主能力都在 `IRegister` 上，注册阶段（`RegisterEventHook`）拿到，出了这个阶段注册器就关闭，再调会抛异常。

## 组件接口

组件能力拆在几个接口里，宿主只依赖接口：

| 接口 | 用在 | 方法 |
| --- | --- | --- |
| `IBaseComponent` | 公共 | `Load` `Reload` `Unload` `Enable` `Disable` `ConfigMap` `UpdateMap` |
| `IComponent` | 公共 | 在 `IBaseComponent` 上加 `Init(string path)` |
| `IBasePlugin` | 插件 | 在 `IComponent` 上加 `RegisterEventHook(IRegister)` |
| `IBaseProxy` | 代理 | 在 `IComponent` 上加 `RegisterEventHook` `CreateInstance()` `Reload(IProxyInstasnce)` |
| `IProxyInstasnce` | 代理实例 | 在 `IBaseComponent` 上加 `Init(path, uuid)` `RegisterEventHook` `Tick()` `SendAsync` |
| `IBaseLib` | 运行库 | `Init` `Load` `Unload` |

- 启停方法都是同步的 `bool`，`Reload`、`UpdateMap`、`RegisterEventHook` 返回 `void`
- `ConfigMap` 返回当前配置项，`UpdateMap` 收面板提交的同一批配置项，插件和代理都有
- 生命周期（`Init → Load → …`）见 [README 的生命周期](../README.md#生命周期)

## 事件

`IRegister` 的方法分三类，看名字就能分清：

| 类别 | 方法 | 说明 |
| --- | --- | --- |
| 注册：把自己的东西暴露出去 | `RegisterPluginEvent(string eventId)` | 返回 `RaiseHandle`，事件全名 `[组件id]事件id`，别的组件按名字订阅 |
| | `RegisterHttpPath(Func<HttpContext, Task>)` | 插件 / 代理是 `/api/{组件id}`，实例是 `/api/{组件id}/{实例uuid}` |
| 订阅：接收别人的事件 | `SubscribeMessage(Func<ProxyMessage, List<BaseMessage>>)` | 代理收到消息后调 `ProxyEventHandle.Dispatch(msg)`，返回值就是要回复的消息 |
| | `SubscribeMessage(Func<MessageEvent, Task>)` | 长任务版：回调在后台跑，不阻塞代理，处理完 `await e.Reply(msg)` 把消息发回发起方 |
| | `SubscribeProxy(Action<ProxyEvent>)` | 实例启用 / 停用时广播 `Started` / `Stopped` |
| | `SubscribePlugin(Action<PluginEvent>)` | 广播订阅：生命周期事件和**所有**自定义事件 |
| | `SubscribePluginEvent(string name, Action<PluginEvent>)` | 定向订阅：只收这个名字的自定义事件，`name` 是 `[组件id]事件id` |
| | `SubscribePluginEvent(string componentId, string eventId, Action<PluginEvent>)` | 定向订阅的另一种写法，用 id 拼名字；两个 id 都可以写 `*` 通配 |
| | `SubscribeSendFilter(Func<ProxyMessage, BaseMessage, Task<bool>>)` | 发送前拦截：消息发到真实平台之前调用，返回 `true` 取消这次发送 |
| 句柄：注册后随时用 | `RegisterState(name, label, description)` | 返回 `StateValueHandle`，`Set(value)` 更新、`Remove()` 删掉 |
| | `RegisterKvState(name, label, description)` | 返回 `StateKvHandle`，只读键值状态，`Set(key, value)` / `Remove(key)` / `Clear()` |
| | `RegisterProxyEvent()` | 返回 `ProxyEventHandle`，代理把收到的消息 `Dispatch(msg)` 注入进来；**只有代理 / 实例能调用** |
| | `RegisterSend()` | 返回 `SendHandle`，插件用它给指定实例发消息；代理 / 实例填自己的代理 id 会抛异常 |

```csharp
public void RegisterEventHook(IRegister register)
{
    register.SubscribeMessage(msg => msg.Message is TextMessage text
        ? [new TextMessage("收到：" + text.Text)]
        : []);

    // 长任务版：慢活放后台，做完自己发回去，代理不会等着
    register.SubscribeMessage(async e =>
    {
        await Task.Delay(3000);

        await e.Reply(new TextMessage("算完了"));
    });

    // 注册自定义事件，全名 [demo]ping，拿着句柄随时触发
    _ping = register.RegisterPluginEvent("ping");

    // 定向订阅：只收 [demo]ping，直接用注册时拿到的全名，不用写死字符串
    register.SubscribePluginEvent(_ping.Name, e => { });

    // 等价写法，跨组件订阅时知道对方的 id 和事件 id 就行
    register.SubscribePluginEvent("demo", "ping", e => { });

    // 通配：所有组件的 ping / demo 的所有事件 / 所有自定义事件
    register.SubscribePluginEvent("*", "ping", e => { });
    register.SubscribePluginEvent("demo", "*", e => { });
    register.SubscribePluginEvent("*", "*", e => { });

    // 广播订阅：生命周期事件和所有自定义事件都会进来
    register.SubscribePlugin(e => { });

    // 发送前拦截：返回 true 这条消息就不会发到平台
    register.SubscribeSendFilter((context, message) =>
        Task.FromResult(message is TextMessage text && text.Text.Contains("敏感词")));

    // 状态句柄，之后随时更新：名字、面板上显示的名字、解释
    _replies = register.RegisterState("replies", "回复条数", "累计回复的消息条数");

    // 只读键值状态：一个名字下面挂一组键值，面板上点开是表格
    _groups = register.RegisterKvState("groups", "群计数", "各群今天已经发出去的条数");

    // 代理 / 实例才有的两个句柄
    _dispatch = register.RegisterProxyEvent();
    _send = register.RegisterSend();
}

// 任意时刻，事件数据只能是基础类型、JsonNode 或 BaseMessage
_ping.Raise("demo");
_ping.Raise(42);
_ping.Raise(new JsonObject { ["value"] = 1 });
_ping.Raise(new TextMessage("带一条消息"));
```

- 两种订阅可以同时挂，互不影响：广播订阅连自定义事件一起收，定向订阅只收匹配的自定义事件、收不到生命周期事件
- 定向订阅的两个 id 支持 `*`：`("demo", "*")` 收 demo 的全部事件，`("*", "ping")` 收所有组件的 ping，`("*", "*")` 等于只收全部自定义事件
- 事件数据只能是**基础类型**（`string` / `char` / `bool` / `byte` / `sbyte` / `short` / `ushort` / `int` / `uint` / `long` / `ulong` / `float` / `double` / `decimal`）、`JsonNode` 或 `BaseMessage`，`RaiseHandle` 对这些各有一个重载，没数据直接 `Raise()`；组件自定义的类型编译期就传不进来
- 原因：每个组件在自己的 `AssemblyLoadContext` 里，传自定义对象会把它的类型和程序集带进别的组件，订阅方只要还拿着这个对象，卸载时就回收不掉
- `PluginEvent.Data` 是 `object?`，值的类型就是 `Raise` 重载的类型，订阅方用 `data is string` / `data is int` / `data is JsonObject` 判断，数字不会变成 `JsonValue`
- 结构化数据还是拼 `JsonObject` / `JsonArray` 传，需要自己的类型再 `Deserialize`；带消息的事件看 `PluginEvent.Message`（`BaseMessage?`），两者二选一
- 事件名可以用 `PluginEventName.Build(组件id, 事件id)` 拼，`PluginEventName.TryParse(name, out var id, out var eventId)` 拆；名字必须是 `[组件id]事件id`，否则订阅时直接抛 `ArgumentException`
- 同一个 owner 重复订阅同一个回调只算一次
- 多个插件都返回消息时按注册顺序拼起来，由调 `ProxyEventHandle.Dispatch` 的代理负责发送
- `RegisterProxyEvent` 只给代理 / 实例用：插件注册直接抛 `组件 {0} 不是代理，不能注册代理事件`
- 回复也走同一条发送链路：代理发回复前先过所有拦截器，任何一个返回 `true` 就不发；拦截器抛异常只记日志，不影响发送
- 拦截器全部放行后，宿主按 `context.Uuid` 找到目标实例，直接调它的 `IProxyInstasnce.SendAsync(message, reply)`
- 长任务版的消息回调是后台跑的，`MessageEvent.Reply` 用的是原始 `ProxyMessage` 的 `Id` / `Uuid`，所以任务结束后回复照样发给发起这次分发的那个实例，也能被其他插件拦截
- 某个回调抛异常只记日志，不影响其他回调
- 实例要收别的代理的消息，也是订阅消息事件，回调里用自己的传输发

## 自定义状态

组件可以把运行时状态上报给面板，组件列表 / 实例列表 / 概览页里都会显示。分两种：

```csharp
// 单值状态：一个名字一个值，基础类型直接传，面板显示成 回复条数: 12
_replies = register.RegisterState("replies", "回复条数", "累计回复的消息条数");
_replies.Set(12);
_replies.Set(true);          // 开关
_replies.Set(0.5);           // 小数
_replies.Set(9000000000L);   // 长整数

// 只读键值状态：一个名字下面一组 键 -> 值，面板上点开是表格
_groups = register.RegisterKvState("groups", "群计数", "各群今天已经发出去的条数");
_groups.Set("群A", 3);
_groups.Set("群B", 5);
_groups.Remove("群A");
_groups.Clear();
```

- 三个参数：`name` 是键（面板接口里用它），`label` 是面板上显示的名字（不填就用 `name`），`description` 是解释，鼠标停在标签上能看到
- 注册时就先把名字和描述记下来，值还没上报时面板上也会显示，值先留空
- 句柄拆成三个：`StateHandle` 是公共基类（`Name` / `Label` / `Description` / `Remove()`），`StateValueHandle` 管单值，`StateKvHandle` 管键值
- 单值状态的 `Set` 有 `string` / `bool` / `char` / `byte` / `sbyte` / `short` / `ushort` / `int` / `uint` / `long` / `ulong` / `float` / `double` / `decimal` 重载，面板按原样显示，数字不会变成字符串
- 键值状态是**只读**的：面板只能看不能改，插件这边随便增删；值同样是任意基础类型
- 状态按组件对象存（插件 / 代理 / 实例各一份），同名覆盖
- 句柄一直留着用，注册阶段结束后照样能用；再调 `RegisterState` / `RegisterKvState` 才会抛异常
- 面板接口 `GET /api/admin/components` 和 `GET /api/admin/instances` 的 `states` 字段形如 `{"replies":{"label":"回复条数","description":"…","kind":"value","value":12},"groups":{"label":"群计数","description":"…","kind":"kv","value":{"群A":3}}}`
- 组件卸载、实例删除时宿主会清掉它上报的状态
- 面板的「概览」页除了组件 / 实例状态面板，底部还有一块「自定义状态」监控：按组件 / 实例自动分组，每项状态一张卡片（和顶部的运行时长同一套样式，显示名字、值和描述），键值状态在卡片里可以展开
- `DemoPlugin` 用回复计数（单值）和各命令被调用的次数（键值）做了示例；QQ 代理把每天各群已发的主动消息条数做成了键值状态 `quota`

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
| `short` | `ConfigShortMap` | 短整数框，带 `Min` / `Max` |
| `ushort` | `ConfigUshortMap` | 无符号短整数框，带 `Min` / `Max` |
| `uint` | `ConfigUintMap` | 无符号整数框，带 `Min` / `Max` |
| `long` | `ConfigLongMap` | 长整数框，带 `Min` / `Max` |
| `ulong` | `ConfigUlongMap` | 无符号长整数框，带 `Min` / `Max` |
| `float` | `ConfigFloatMap` | 小数框，带 `Min` / `Max` |
| `double` | `ConfigDoubleMap` | 双精度小数框，带 `Min` / `Max` |
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

## 注册 http 接口

回调是 `Func<HttpContext, Task>`，宿主会 `await` 它：

```csharp
// 只能在 RegisterEventHook 阶段注册
register.RegisterHttpPath(HandleAsync);    // 插件 / 代理是 /api/{组件id}，实例是 /api/{组件id}/{实例uuid}

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

## 调用宿主接口

`ComponentApi` 是宿主暴露给组件的静态入口，任何组件随时能调：

| 方法 | 说明 |
| --- | --- |
| `Lang` | 宿主当前语言（`zh_cn` / `en_us`），组件做多语言时读它 |
| `GetComponents()` | 全部组件（含磁盘上还没加载的），返回 `ComponentInfo`：`Id` / `Name` / `Version` / `Type` / `Description` / `Folder` / `Loaded` / `Enabled` / `Blocked` / `State` |
| `GetInstances()` | 全部实例（含磁盘上还没加载的），返回 `InstanceInfo`：`Uuid` / `Proxy` / `Name` / `Folder` / `Loaded` / `Enabled` |
| `GetComponentState(string id)` | 组件状态：`enabled` / `disabled` / `loaded` / `unloaded` / `blocked` |
| `StartComponent(string id)` | 启动组件：没加载先加载，然后启用；运行库只加载 |
| `DisableComponent(string id)` | 禁用组件，只停用不卸载；运行库返回 `false` |
| `ReloadComponent(string id)` | 重启组件：停用 → 重读配置 → 按原来的启用状态恢复 |
| `GetComponentConfig(string id)` | 组件的配置项，没提供或者没加载返回 `null` |
| `UpdateComponentConfig(string id, map)` | 把新配置交给组件的 `UpdateMap` |
| `EnableInstance(Guid uuid)` | 启用实例，没加载会先从磁盘加载 |
| `DisableInstance(Guid uuid)` | 禁用实例，只停用不卸载 |
| `ReloadInstance(Guid uuid)` | 重启实例：停用 → 重读配置 → 恢复原来的启用状态 |
| `GetInstanceConfig(Guid uuid)` | 实例的配置项，没有返回 `null` |
| `UpdateInstanceConfig(Guid uuid, map)` | 把新配置交给实例的 `UpdateMap` |

```csharp
// 遍历实例，把没启用的都启用起来
foreach (var item in ComponentApi.GetInstances())
{
    if (!item.Enabled)
    {
        ComponentApi.EnableInstance(item.Uuid);
    }
}
```

- 返回 `bool` 的方法失败时返回 `false`，原因写在宿主日志里；id / uuid 不存在不会抛异常（只有 id 为空才抛 `ArgumentException`）
- 启用 / 禁用 / 重启都不会卸载程序集，组件在自己的回调里操作自己也是安全的

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
- 组件禁用 / 卸载、实例禁用 / 删除时，宿主调用 `ServerRegistry.StopAll(owner)` 关掉它名下的全部服务器
- `ComponentServer.Start()` 同步执行，成功返回 `true`；`Stop()` 可以重复调用

## 证书

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
- **选择证书**：`ServerOptions.Certificate` 填名字，也兼容直接填 pfx 路径

```json
{
  "Items": [
    { "Name": "panel", "File": "panel.pfx" }
  ]
}
```

- 名字找不到时会把 `Certificate` 当成本地 pfx 路径，这时密码用同一条配置里的 `CertificatePassword`
- 名字或路径都解析不到时 `Start()` 返回 `false` 并记一条「找不到证书」日志，不会静默退回 http
- 证书本身要带 Server Authentication 用途（IIS 导出的证书默认就带），否则 Kestrel 会拒绝启动
- 想知道有哪些证书可用：`CertStore.Names()` 拿名字列表，`CertStore.List()` 拿带路径和密码的完整信息
- 面板接口 `GET /api/admin/certs` 只返回名字和文件名，不会返回密码
