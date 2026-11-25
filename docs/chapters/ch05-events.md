# 第五章：事件系统

前面几章都在写单个组件。这一章讲组件之间怎么说话，也就是 `IRegister` 这一套事件系统：能收什么、能发什么、句柄怎么用。

自定义事件只是其中一小块，另外还有消息事件、插件生命周期事件、代理事件、发送拦截和发送句柄。

## 1. 三个方向

`IRegister` 的方法按用途分三类：

| 方向 | 方法 | 拿到的句柄 |
| --- | --- | --- |
| 注册：把自己的东西暴露出去 | `RegisterPluginEvent(eventId)` | `RaiseHandle` |
| | `RegisterHttpPath(handler)` | 无（见[第十二章](ch12-http.md)） |
| 订阅：接收别人的事件 | `SubscribeMessage(...)`（同步 / 长任务） | 无 |
| | `SubscribeProxy(Action<ProxyEvent>)` | 无 |
| | `SubscribePlugin(Action<PluginEvent>)` | 无 |
| | `SubscribePluginEvent(name / id, eventId, ...)` | 无 |
| | `SubscribeSendFilter(...)` | 无 |
| 句柄：注册后随时用 | `RegisterState` / `RegisterKvState` | `StateValueHandle` / `StateKvHandle`（见[第七章](ch07-state.md)） |
| | `RegisterSend()` | `SendHandle` |
| | `RegisterProxyEvent()` | `ProxyEventHandle`（只有代理 / 实例能拿） |

注册器只在 `RegisterEventHook` 阶段有效，出了这个阶段再调会抛异常；句柄可以一直留着用。

## 2. 消息事件

代理收到消息后调 `Dispatch(message)`，所有订阅了消息事件的组件都会收到：

```csharp
// 同步版：返回值就是要回复的消息
register.SubscribeMessage(message =>
    message.Message is TextMessage text && text.Text == "/ping"
        ? [new TextMessage("pong")]
        : []);

// 长任务版：回调在后台跑，处理完自己发回去
register.SubscribeMessage(async e =>
{
    var result = await SlowWorkAsync(e.Message);

    await e.Reply(new TextMessage(result));
});
```

- 同步版要**尽快返回**，返回值是 `List<BaseMessage>`，空列表就是不回
- 长任务版不阻塞代理，`MessageEvent.Reply` 可以调多次，任务结束后也能调
- 多个组件都返回消息时，按注册顺序拼起来交给发起这次分发的代理实例
- 某个回调抛异常只记日志，不影响其他组件
- 插件和代理都能订阅；实例订阅了就能收别的代理的消息

## 3. 插件事件

插件事件有两类内容：生命周期和自定义事件。

```csharp
// 广播订阅：生命周期 + 所有自定义事件
register.SubscribePlugin(e =>
{
    switch (e.Type)
    {
        case PluginEventType.Loaded:
        case PluginEventType.Enabled:
        case PluginEventType.Disabled:
        case PluginEventType.Unloaded:
        case PluginEventType.Custom:      // 这里才看 e.Name / e.Data
    }
});

// 定向订阅：只收指定事件，收不到生命周期
register.SubscribePluginEvent("demo", "ping", e => { });

// 两个 id 都支持通配
register.SubscribePluginEvent("demo", "*", e => { });   // demo 的全部事件
register.SubscribePluginEvent("*", "ping", e => { });   // 所有组件的 ping
register.SubscribePluginEvent("*", "*", e => { });      // 所有自定义事件
```

- `PluginEvent` 有 `Id`（来源组件）、`Type`、`Name`（自定义事件全名）、`Data`、`Message`
- 定向订阅收不到生命周期事件，广播订阅两样都收
- 事件名是 `[组件id]事件id`，可以用 `PluginEventName.Build` 拼、`TryParse` 拆

## 4. 自定义事件

自定义事件是「组件自己定义、别人按名字订阅」的广播，核心就三步：

```csharp
// 1. 注册，拿到触发句柄
_ping = register.RegisterPluginEvent("ping");          // 全名 [echo]ping

// 2. 订阅（本组件或别的组件都行）
register.SubscribePluginEvent(_ping.Name, e => Logger.Info($"收到 {e.Name}"));

// 3. 随时触发
_ping.Raise("demo");                 // 字符串
_ping.Raise(42);                     // 整数
_ping.Raise(new JsonObject { ["value"] = 1 });
_ping.Raise(new TextMessage("带一条消息"));
_ping.Raise();                       // 没有数据
```

能传的数据是有限的：基础类型（`string` / `char` / `bool` / `byte` / `sbyte` / `short` / `ushort` / `int` / `uint` / `long` / `ulong` / `float` / `double` / `decimal`）、`JsonNode`、`BaseMessage`。

`PluginEvent.Data` 是 `object?`，收到的人直接模式匹配：

```csharp
register.SubscribePluginEvent("echo", "ping", e =>
{
    switch (e.Data)
    {
        case null: break;
        case string text: break;
        case int number: break;
        case bool flag: break;
        case JsonObject obj: break;
    }
});
```

**不能传自己的类型**：每个组件在自己的 `AssemblyLoadContext` 里，传自定义对象会把类型和程序集带进别的组件，卸载时回收不掉。结构化数据用 `JsonObject` / `JsonArray`，需要强类型就自己 `Deserialize`。

## 5. 代理事件

实例启用 / 停用时广播 `ProxyEvent`：

```csharp
register.SubscribeProxy(e =>
{
    // e.Type 是 Started 或 Stopped
    // e.ProxyId / e.Uuid 说明是哪个代理的哪个实例
});
```

适合做「代理连上了再开始干活」：比如等 QQ 实例起来后再推送消息。

## 6. 发送拦截

消息真正发到平台之前，会先过所有订阅者的拦截器：

```csharp
register.SubscribeSendFilter((context, reply) =>
{
    // 返回 true 就取消这次发送
    var block = reply is TextMessage text && text.Text.Contains("敏感词");

    return Task.FromResult(block);
});
```

- 参数是 `ProxyMessage`（发往哪个会话）和 `BaseMessage`（要发的内容）
- 任意一个拦截器返回 `true` 就取消，其余拦截器照样会被调用
- 拦截器抛异常只记日志，按放行处理
- 插件和代理的回复、主动发送都会经过它

## 7. 发送句柄

`SendHandle` 用来主动发消息，参数是上下文 + 内容：

```csharp
_send = register.RegisterSend();

var context = new ProxyMessage
{
    Id = "qq",                       // 目标代理
    Uuid = instanceUuid,             // 目标实例
    SenderType = MessageSenderType.Group,
    Sender = "system",
    Target = "group",
    Message = new TextMessage("")
};

await _send.SendAsync(context, new TextMessage("主动消息"));
```

- 发出去之前同样过发送拦截器
- 插件拿到的句柄可以发给任意代理；代理 / 实例拿到的句柄不能填自己的代理 id
- 不知道实例 uuid 时查 `ComponentApi.GetInstances()`（见[第十一章](ch11-componentapi.md)）

## 8. 代理注入句柄

只有代理和实例能拿 `ProxyEventHandle`，用来把收到的消息注入宿主：

```csharp
_dispatch = register.RegisterProxyEvent();

var replies = _dispatch.Dispatch(message);   // 同步，返回所有组件的回复
```

插件调用会直接抛 `组件 {0} 不是代理，不能注册代理事件`。

## 9. 执行顺序和异常

| 事件 | 顺序 | 异常处理 |
| --- | --- | --- |
| 消息事件 | 按订阅顺序，结果按顺序拼接 | 记日志，继续后面的 |
| 自定义事件 | 按订阅顺序同步广播 | 记日志，继续后面的 |
| 发送拦截 | 按订阅顺序，碰到 `true` 就取消 | 记日志，按放行处理 |
| 生命周期 | 宿主触发 | 记日志 |

- 同一个 owner 重复订阅同一个回调只算一次
- 事件是同步的：触发方要等所有订阅者返回，所以回调里别做长任务
- 组件卸载时，它注册 / 订阅的东西会按 owner 自动摘掉

## 10. 小结

| 想干什么 | 用哪个 |
| --- | --- |
| 处理收到的消息 | `SubscribeMessage` |
| 监听组件加载 / 启用 / 卸载 | `SubscribePlugin`（看 `Type`） |
| 和别的组件约定一个广播 | `RegisterPluginEvent` + `SubscribePluginEvent` |
| 等代理实例起来 | `SubscribeProxy` |
| 过滤 / 屏蔽要发出去的消息 | `SubscribeSendFilter` |
| 主动发消息 | `RegisterSend` |
| 代理把消息注入宿主 | `RegisterProxyEvent` |
| 对外提供 http 接口 | `RegisterHttpPath`（[第十二章](ch12-http.md)） |

完整方法签名见[接口与 API 参考](../reference/api.md#事件)。
---

上一章：[第四章 · 写一个运行库](ch04-lib.md) ｜ [返回目录](../README.md) ｜ 下一章：[第六章 · 配置文件](ch06-config.md)
