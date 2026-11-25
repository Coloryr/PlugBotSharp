# 参考：代理实现

教程式的代理写法见[第三章](../chapters/ch03-proxy.md)。

代理接一个平台：登录、收事件、把消息翻译成统一模型交给插件，再把插件返回的消息翻译成平台格式发出去。代理里不写业务逻辑。

例子：[ProxyDemo](../../Proxy/ProxyDemo)，一个用 http 收发消息的完整代理，代码不到 300 行。

## 接口

代理分两层：组件（`IBaseProxy`）管配置和创建实例，实例（`IProxyInstasnce`）管一个具体账号的连接和收发。

```csharp
public class ProxyMain : IBaseProxy
{
    public bool Init(string path) { return true; }          // path 是 Configs/{组件id}
    public void RegisterEventHook(IRegister register) { }    // 组件级订阅，一般空着
    public IProxyInstasnce CreateInstance() => new DemoInstance(Meta.Info.Id);
    public bool Load() / bool Unload() / bool Enable() / bool Disable() { return true; }
    public void Reload() { }                                 // 重读组件配置
    public void Reload(IProxyInstasnce instasnce) { }        // 把新配置推给实例
    public Dictionary<string, ConfigMap> ConfigMap() => new();
    public void UpdateMap(Dictionary<string, ConfigMap> data) { }
}

public class DemoInstance : IProxyInstasnce
{
    public void Init(string path, Guid uuid) { }             // path 是 Instances/{uuid}
    public bool Load() { return true; }
    public void RegisterEventHook(IRegister register) { }    // 拿分发句柄、注册 http 路径、报状态
    public bool Enable() { return true; }
    public bool Disable() { return true; }
    public bool Unload() { return true; }
    public void Tick() { }                                   // 每秒调一次，做重连、重试
    public Task SendAsync(ProxyMessage message, BaseMessage reply) { return Task.CompletedTask; }
    public Dictionary<string, ConfigMap> ConfigMap() => new();
    public void UpdateMap(Dictionary<string, ConfigMap> data) { }
}
```

| 阶段 | 说明 |
| --- | --- |
| 代理 `Init → Load → Enable → Disable → Unload` | 代理组件本身的生命周期 |
| 实例 `Init → Load → RegisterEventHook → Enable → Tick(1s) → Disable` | 每个实例一条 |
| 重载代理配置 | `Disable` → `Reload` → `Enable` |
| 重载实例配置 | `Disable 实例` → `Reload(IProxyInstasnce)` → `Reload 实例` → `Enable 实例` |

- 代理组件启用后才能创建实例；组件禁用时它的实例会全部停掉
- 实例的 `Init(path, uuid)` 收到的是实例目录 `Instances/{uuid}`，账号配置写这里
- 组件配置和实例配置都可以用面板表单，见 [api.md](./api.md#组件和实例的设置)

## 消息模型

代理收到平台消息后，先转成 `ProxyMessage` 再交给宿主：

| 字段 | 说明 |
| --- | --- |
| `Id` | 代理组件 id，一般用 `Meta.Info.Id` |
| `Uuid` | 实例 uuid，回复会发回这个实例 |
| `SenderType` | `Group` / `User` / `Guild` / `GuildUser` / `Any` |
| `Sender` | 发送者 id（QQ 的 `member_openid`、OneBot 的 `user_id`） |
| `Target` | 回复目标，群消息填群号，私聊留空（回给 `Sender`） |
| `Message` | `BaseMessage`，文本、图片、列表都行 |
| `ReplyId` | 平台消息 id，被动回复 / 引用回复时带上 |
| `ReplyEventId` | 平台事件 id，频道事件用 |

插件返回的是 `BaseMessage`，代理按 `message.Type` 分派：

```csharp
switch (reply)
{
    case TextMessage text: /* 发文本 */ break;
    case ListMessage list: /* 拆成多条 */ break;
    case RawMessage raw: /* 平台专属 payload */ break;
}
```

## 分发和回复

实例在 `RegisterEventHook` 里拿两个句柄：

```csharp
public void RegisterEventHook(IRegister register)
{
    // 收到消息时把 ProxyMessage 注入进来，返回值是要回的消息
    _dispatch = register.RegisterProxyEvent();

    // 需要被外部调用时注册 http 路径（插件主动发的消息会走实例的 SendAsync）

    register.RegisterHttpPath(HandleAsync);
}
```

回复链路：

1. 代理调 `_dispatch.Dispatch(message)`，宿主把所有订阅消息事件的插件（和别的代理）跑一遍
2. 收集到的 `List<BaseMessage>` 原样返回给代理
3. 代理自己按平台格式发出去；插件主动发的消息则走另一条：`SendHandle.SendAsync` → 所有发送拦截器 → 目标实例的 `SendAsync`

- `Dispatch` 是**同步**的，收到的消息要尽快转进去，别在代理里排队太久
- 发送拦截器：任何插件返回 `true` 就取消这次发送，代理不需要关心
- 长任务版插件会在后台慢慢处理，之后通过 `MessageEvent.Reply` 回来，代理不用等
- `SendAsync` 是给别人用的入口，填自己的代理 id 会抛异常（除了发给自己这个实例）

## ProxyDemo 讲解

`ProxyDemo` 用一个 http 接口当"平台"：外部程序 POST 一条消息进来，插件处理完再把回复 POST 回去。麻雀虽小，代理要做的几件事它都做了。

| 文件 | 作用 |
| --- | --- |
| `Meta.cs` | 组件清单，`Type = Proxy`，id `proxydemo` |
| `ProxyMain.cs` | 组件级配置 `LogMessage`、创建实例 |
| `DemoInstance.cs` | 收发消息、状态上报、实例配置 |
| `DataObjs.cs` | 配置和报文模型，带 `JsonSerializerContext` |

### 收消息

实例注册一个 http 路径 `/api/proxydemo/{实例uuid}`，外部程序 POST：

```json
{
  "senderType": "group",
  "sender": "u1",
  "target": "g1",
  "replyId": "m1",
  "text": "/text",
  "callback": "http://127.0.0.1:8080/reply"
}
```

代理把它转成 `ProxyMessage`，`Dispatch` 给宿主，再把插件返回的消息发回去：

```csharp
var message = new ProxyMessage
{
    Id = _proxyId,
    Uuid = _uuid,
    SenderType = ParseSenderType(request.SenderType),
    Sender = request.Sender,
    Target = request.Target,
    ReplyId = request.ReplyId,
    Message = new TextMessage(request.Text)
};

var replies = _dispatch?.Dispatch(message) ?? [];
```

### 回消息

`callback` 填了（或者实例配置里配了 `Callback`）就把回复 POST 过去；没填就放进 http 响应里返回：

```json
{
  "ok": true,
  "message": "",
  "replies": [
    { "sender": "u1", "target": "g1", "text": "这是一条文本消息" }
  ]
}
```

插件主动发的消息走实例的 `SendAsync`，同样发到 `Callback`。

### 配置

| 层级 | 配置 | 说明 |
| --- | --- | --- |
| 组件 | `LogMessage` | 把收发内容写进调试日志 |
| 实例 | `Callback` | 回复发到这个地址，留空则放回 http 响应 |
| 实例 | `TimeoutSeconds` | 回调超时，1 ~ 60 秒 |

### 状态

| 名字 | 类型 | 说明 |
| --- | --- | --- |
| `recv` | 单值 | 推进来的消息条数 |
| `sent` | 单值 | 发回去的回复条数 |
| `senders` | 键值 | 发送者 → 条数 |

### 试一下

装好 `ProxyDemo.zip`、启用组件、建一个实例，然后：

```bash
curl -X POST http://127.0.0.1:10000/api/proxydemo/{实例uuid} ^
  -H "Content-Type: application/json" ^
  -d "{\"senderType\":\"group\",\"sender\":\"u1\",\"target\":\"g1\",\"text\":\"/text\"}"
```

`DemoPlugin` 收到 `/text` 会回一条文本，响应里的 `replies` 就是它。

## 现有代理

### QQ 代理

- 收到 `GROUP_AT_MESSAGE_CREATE` / `GROUP_MESSAGE_CREATE`（群）和 `C2C_MESSAGE_CREATE`（单聊）时转成消息事件，其他事件只记日志
- 群消息：`SenderType = Group`、`Sender = member_openid`、`Target = group_openid`、`ReplyId = 平台消息 id`；单聊 `Target` 为空，回复时用 `Sender`
- 回复走被动回复：带 `msg_id` 和按消息 id 自增的 `msg_seq`，群里发到 `v2/groups/{group_openid}/messages`，单聊发到 `v2/users/{openid}/messages`
- `HookType` 三种：`WebHook` 注册到宿主（`/api/qqproxy/{实例uuid}`）；`WebHost` 自己起 Kestrel，监听实例配置里的 `Port`；`WebSocket` 还没实现

### OneBot 代理

实例配置拆成四个开关：`Transport`（`WebSocket` / `Http`）、`Role`（`Client` / `Server`）、`Receive`、`Send`。

| Transport | Role | 收事件 | 发消息 |
| --- | --- | --- | --- |
| `WebSocket` | `Client` | 连 `Url` 的 socket | 同一条 socket |
| `WebSocket` | `Server` | 机器人连 `Host:Port` 的 `/ws` | `Url` 上的 HTTP API |
| `Http` | `Server` | 机器人 POST 到 `Host:Port` 的 `/` | `Url` 上的 HTTP API |
| `Http` | `Client` | 收不到 | `Url` 上的 HTTP API |

- `Receive` / `Send` 各管一半：关掉 `Send` 就是只收，关掉 `Receive` 就是只发
- 服务端模式用 `Token` 校验 `Authorization: Bearer`；客户端 WebSocket 断线由 `Tick` 按 `ReconnectSeconds` 重连
- 协议版本 `V11` / `V12`：段结构一样，事件字段和 action 不同（V12 用 `type` + `detail_type`，id 是字符串）
- OneBot 专属的段（音乐、位置、名片、戳一戳、骰子、合并转发）用 `OneBotSegment.Raw(...)` 包成 `RawMessage` 发

### GsCore 代理

`gsuid_core` 把原神功能跑在独立进程里，这个代理和它双向收发：

```
QQ / OneBot 实例 ──消息事件──▶ GsCore 代理 ──MessageReceive──▶ gsuid_core
                                GsCore 代理 ◀──MessageReceive── gsuid_core
```

- 连 `{Url}/ws/{BotId}`，有 `Token` 就带 `?token=`；断线由 `Tick` 按 `ReconnectSeconds` 重连
- 发：订阅所有平台的消息事件，转成 `MessageReceive`（文本、@、图片、语音、视频、markdown、引用）
- 收：core 下发的报文按 `GsMessageReceive` 解析，段还原成统一消息，再拼 `ProxyMessage` 注入
- 回环保护：`Forward` 只转发别的代理的消息，自己 `Dispatch` 出去的消息不会再转一遍

## 写代理的注意

- `Dispatch` 不要在 UI 线程或锁里调，插件回调可能很慢
- `Tick` 每秒一次，只做重连 / 重试这类轻活，别阻塞
- 禁用实例时把连接、定时器、服务器都停掉；宿主也会帮你关掉 `ComponentApi.StartServer` 起的服务器
- 代理不解析业务，也不查数据库，插件要的数据自己带
- 同一个 `ProxyMessage` 只 `Dispatch` 一次，重复注入会让插件回两遍

