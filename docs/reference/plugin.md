# 参考：插件与消息

教程式的插件写法见[第二章](../chapters/ch02-plugin.md)和[第五章](../chapters/ch05-events.md)。

插件处理消息：收到消息、判断要不要回、回什么。插件看不到平台协议，用的是统一消息模型，所以同一个插件在 QQ、OneBot、gsuid_core 上都能跑。

例子：[DemoPlugin](../../Plugin/DemoPlugin)，里面每个命令对应一种消息体。

## 接口和生命周期

```csharp
public class PluginMain : IBasePlugin
{
    public bool Init(string path) { return true; }         // path 是 Configs/{组件id}
    public bool Load() { return true; }
    public void RegisterEventHook(IRegister register) { }   // 只能在这里注册 / 订阅
    public bool Enable() { return true; }
    public bool Disable() { return true; }
    public bool Unload() { return true; }

    public Dictionary<string, ConfigMap> ConfigMap() => new();
    public void UpdateMap(Dictionary<string, ConfigMap> data) { }
}
```

| 阶段 | 说明 |
| --- | --- |
| `Init(path)` | 只调一次，参数是组件配置目录 `Configs/{组件id}`，配置里的文件写这里 |
| `Load()` | 加载组件，读配置 |
| `RegisterEventHook(register)` | 注册 / 订阅事件，过了这个阶段注册器关闭 |
| `Enable()` / `Disable()` | 面板启用 / 禁用，禁用时宿主会摘掉它注册的 http 接口 |
| `Unload()` | 卸载，之后组件目录可以删除 |
| `Reload()` | 面板重载配置：`Disable` → `Reload` → `Enable` |

## 订阅消息

两种写法，可以同时挂：

```csharp
public void RegisterEventHook(IRegister register)
{
    // 同步版：返回要回复的消息，不回复就给空列表
    register.SubscribeMessage(message =>
        message.Message is TextMessage text && text.Text == "/ping"
            ? [new TextMessage("pong")]
            : []);

    // 长任务版：回调在后台跑，不阻塞代理，处理完自己发回去
    register.SubscribeMessage(async e =>
    {
        var result = await SlowWorkAsync(e.Message);

        await e.Reply(new TextMessage(result));
    });
}
```

- 同步版返回的 `List<BaseMessage>` 会交给发起这次分发的代理实例发出去，多个插件的结果按注册顺序拼起来
- 长任务版适合要等接口、查数据库的活，`MessageEvent.Reply` 可以调多次，任务结束后也能调
- 某个回调抛异常只记日志，其它回调照常执行
- `ProxyMessage` 里有 `Id`（代理组件 id）、`Uuid`（实例）、`SenderType`、`Sender`、`Target`、`ReplyId`，需要引用回复时带上 `ReplyId`
- 只处理自己关心的消息：判断 `message.Message` 的类型和文本，别对所有消息都回

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

## DemoPlugin 的命令

[DemoPlugin](../../Plugin/DemoPlugin) 里每个命令对应一种消息体，可以直接当模板抄：

| 命令 | 发出的消息 |
| --- | --- |
| `/text` | `TextMessage`，内容可以在面板的设置里改 |
| `/markdown` | `MarkdownMessage`，QQ 侧转成 `msg_type: 2`，OneBot 侧转成 `markdown` 段 |
| `/keyboard` | markdown + 内嵌键盘（指令按钮 + 跳转按钮） |
| `/card` | `msg_type: 3` 图文卡片 |
| `/list` | `ListMessage`，代理会拆成多条发送 |
| `/reply` | `ListMessage(ReplyMessage, TextMessage)`，引用收到的消息回复 |
| `/event` | 触发自己的自定义事件 `[demo]ping` |
| `/block` | 返回一条会被拦截器拦下的消息，用来验证发送拦截 |

插件同时把状态报给面板：`pings` / `events` / `replies` / `blocked` 是单值状态，`commands` 是键值状态（命令 → 次数）。

## 主动发消息

插件也可以不等消息、自己发：在注册阶段拿 `RegisterSend()` 的句柄，随时调：

```csharp
_send = register.RegisterSend();

// 任意时刻，给某个实例发一条
var context = new ProxyMessage
{
    Id = "qq",                        // 目标代理组件 id
    Uuid = instanceUuid,              // 目标实例
    SenderType = MessageSenderType.Group,
    Sender = "user",
    Target = "group",
    Message = new TextMessage("")     // 只是上下文，真正发出去的是第二个参数
};

await _send.SendAsync(context, new TextMessage("主动发出去的消息"));
```

- 发出去之前同样会过所有插件的发送拦截器
- 从插件里拿到的句柄可以发给任意代理；代理 / 实例拿到的句柄不能填自己的代理 id，填了直接抛异常
- 不知道实例 uuid 时可以用 `ComponentApi.GetInstances()` 查（见 [api.md](./api.md)）

## 配置、状态和事件

- 配置项：`ConfigMap()` 声明，`UpdateMap()` 接收，面板自动生成表单，见 [api.md](./api.md#组件和实例的设置)
- 状态：`RegisterState` / `RegisterKvState`，面板上显示成标签和表格，见 [api.md](./api.md#自定义状态)
- 自定义事件：`RegisterPluginEvent` 注册，别的组件按 `[组件id]事件id` 订阅，见 [api.md](./api.md#事件)

## 注意

- 插件在各自的 `AssemblyLoadContext` 里，跨组件传对象只有基础类型、`JsonNode` 和 `BaseMessage` 是安全的
- 同步回调里别做耗时操作，会卡住代理那条线程，慢活放进长任务版
- 插件卸载时它注册的 http 接口、事件订阅、服务器都会自动摘掉，不用手动清理

