# 第三章：写一个代理

代理接一个平台：登录、收事件、把消息翻译成统一模型，再把插件返回的消息翻译成平台格式发出去。**代理里不写业务逻辑。**

这一章按顺序把代理写出来，代码都是可以直接跑的骨架，完整实现看 [ProxyDemo](../../Proxy/ProxyDemo)。

## 1. 代理要做的三件事

```
平台协议（http / ws / 长连接） ──▶ 代理 ──▶ ProxyMessage ──▶ Dispatch ──▶ 插件
                                   ▲                                   │
                                   └────── 平台报文 ◀── BaseMessage ◀────┘
```

1. **收**：把平台的消息转成 `ProxyMessage`
2. **转**：调用 `Dispatch(message)`，拿到插件要回的 `List<BaseMessage>`
3. **发**：把 `BaseMessage` 转回平台格式发出去；插件主动发的消息走 `SendAsync`

## 2. 建工程

```
Proxy/EchoProxy/
  EchoProxy.csproj
  Meta.cs
  ProxyMain.cs          组件：配置、创建实例
  EchoInstance.cs       实例：连接、收发、状态
  DataObjs.cs           报文模型 + JsonSerializerContext（可选）
```

`csproj` 和插件一样：引用 `PlugBotSharp.Api`、引用 `MetaGen` 但不带 dll、Import 打包目标。

## 3. 清单 Meta.cs

```csharp
public static class Meta
{
    public static MetaObj Info = new()
    {
        Name = "EchoProxy",
        Id = "echoproxy",
        Version = "1.0.0",
        Description = "示例代理",
        Type = ComponentType.Proxy,
        Dlls = ["EchoProxy.dll"],
        Deps = []
    };
}
```

`Type` 必须是 `ComponentType.Proxy`，只有代理能被创建实例。

## 4. 组件：ProxyMain

```csharp
public class ProxyMain : IBaseProxy
{
    private string _path = "";
    private ProxyConfigObj _config = new();

    public bool Init(string path)          // Configs/echoproxy
    {
        _path = path;
        ReadConfig();

        return true;
    }

    public bool Load() => true;
    public bool Unload() => true;
    public bool Enable() => true;
    public bool Disable() => true;

    public void Reload() => ReadConfig();

    // 组件级订阅，一般空着
    public void RegisterEventHook(IRegister register) { }

    // 一个实例 = 一个账号 / 一条连接
    public IProxyInstasnce CreateInstance() => new EchoInstance(Meta.Info.Id);

    // 组件配置变了，推给已有实例
    public void Reload(IProxyInstasnce instasnce) { }

    public Dictionary<string, ConfigMap> ConfigMap() => new()
    {
        ["LogMessage"] = new ConfigBoolMap { Key = "LogMessage", Value = _config.LogMessage, Default = false }
    };

    public void UpdateMap(Dictionary<string, ConfigMap> data)
    {
        if (data.TryGetValue("LogMessage", out var item) && item is ConfigBoolMap map)
        {
            _config.LogMessage = map.Value;
        }
    }

    private void ReadConfig() { /* 读 Configs/echoproxy/Config.json */ }
}
```

## 5. 实例：连平台、收消息

实例是代理的核心，一个实例对应一个账号：

```csharp
public class EchoInstance : IProxyInstasnce
{
    private readonly string _proxyId;
    private Guid _uuid;
    private bool _enabled;

    private ProxyEventHandle? _dispatch;      // 收消息后注入
    private StateValueHandle? _recvState;
    private int _recv;

    public EchoInstance(string proxyId) => _proxyId = proxyId;

    public void Init(string path, Guid uuid)   // Instances/{uuid}
    {
        _uuid = uuid;
    }

    public bool Load() => true;
    public void Reload() { }
    public bool Unload() => true;
    public bool Enable() { _enabled = true; return true; }
    public bool Disable() { _enabled = false; return true; }
    public void Tick() { }                     // 每秒一次：重连、重试

    public void RegisterEventHook(IRegister register)
    {
        _dispatch = register.RegisterProxyEvent();
        register.RegisterHttpPath(HandleAsync);

        _recvState = register.RegisterState("recv", "收到条数", "平台推进来的消息条数");
    }

    // 平台来了消息（http / ws 回调里都行）
    private void OnPlatformMessage(string sender, string target, string text, string replyId)
    {
        var message = new ProxyMessage
        {
            Id = _proxyId,
            Uuid = _uuid,
            SenderType = MessageSenderType.Group,
            Sender = sender,
            Target = target,
            ReplyId = replyId,
            Message = new TextMessage(text)
        };

        _recvState?.Set(++_recv);

        var replies = _dispatch?.Dispatch(message) ?? [];

        foreach (var reply in replies)
        {
            SendToPlatform(target, Describe(reply));
        }
    }

    // http 入口：把报文转成一条消息（真实代理这里换成 ws / 长连接的回调）
    private async Task HandleAsync(HttpContext ctx)
    {
        var request = await JsonSerializer.DeserializeAsync<RequestObj>(ctx.Request.Body) ?? new RequestObj();

        OnPlatformMessage(request.Sender, request.Target, request.Text, request.ReplyId);

        await ctx.Response.WriteAsJsonAsync(new { ok = true });
    }

    // 桩：真正发到平台，换成你自己的发送实现
    private void SendToPlatform(string target, string text) { }

    private static string Describe(BaseMessage message) => message switch
    {
        TextMessage text => text.Text,
        MarkdownMessage markdown => markdown.Content,
        ListMessage list => string.Join(" ", list.Messages.Select(Describe)),
        _ => `[{message.Type}]`
    };
}
```

- `Dispatch` 是**同步**的，插件回调可能很慢，别在锁里调
- 返回 `List<BaseMessage>`，按 `message.Type` 分派到平台格式（文本、图片、列表……）
- 平台发不了的类型直接丢，不要抛异常

```csharp
/// <summary>
/// http 报文模型
/// </summary>
public class RequestObj
{
    public string Sender { get; set; } = "";
    public string Target { get; set; } = "";
    public string Text { get; set; } = "";
    public string ReplyId { get; set; } = "";
}
```

## 6. 插件主动发的消息：SendAsync

```csharp
public async Task SendAsync(ProxyMessage message, BaseMessage reply)
{
    var target = string.IsNullOrEmpty(message.Target) ? message.Sender : message.Target;

    await SendToPlatformAsync(target, Describe(reply));
}
```

调用链是：插件 `SendHandle.SendAsync` → 所有发送拦截器 → 目标实例的 `SendAsync`。拦截器已经放行，实例只管发。

## 7. 实例配置

```csharp
public Dictionary<string, ConfigMap> ConfigMap() => new()
{
    ["Callback"] = new ConfigStringMap
    {
        Key = "Callback",
        Value = _data.Callback,
        Default = "",
        Description = "回复发到这个地址"
    },
    ["TimeoutSeconds"] = new ConfigIntMap
    {
        Key = "TimeoutSeconds",
        Value = _data.TimeoutSeconds,
        Min = 1, Max = 60, Default = 10
    }
};
```

实例配置存在 `Instances/{uuid}/config.json`，由实例自己读写；面板保存后宿主会调 `Reload`。

## 8. 打包和测试

```bash
dotnet build Proxy/EchoProxy/EchoProxy.csproj
```

产物 `EchoProxy.zip`，装进面板 → 启用组件 → 新建实例 → 调用实例的 http 路径：

```bash
curl -X POST http://127.0.0.1:10000/api/echoproxy/{实例uuid} ^
  -H "Content-Type: application/json" ^
  -d "{\"sender\":\"u1\",\"target\":\"g1\",\"text\":\"/text\"}"
```

能看到插件回的文本就说明「收 → 转 → 发」全通了。

## 9. 常见坑

| 现象 | 原因 |
| --- | --- |
| 插件收不到消息 | 忘了 `Dispatch`，或者 `ProxyEventHandle` 没在 `RegisterEventHook` 里拿 |
| 回复发给了错误的实例 | `ProxyMessage.Uuid` 填错，宿主按它找实例 |
| 插件主动发消息被拒 | `SendHandle` 填了自己的代理 id 且不是自己这个实例 |
| 禁用后还在发 | `Disable` 里没停连接 / 定时器 |
| 消息重复 | 同一个 `ProxyMessage` `Dispatch` 了两次，或者转发时没做回环保护 |
| 卡住整个机器人 | `Dispatch` 或 `Tick` 里做了耗时操作 |

现有代理（QQ / OneBot / GsCore）的实现细节见 [proxy.md](../reference/proxy.md)。

---

上一章：[第二章 · 完整插件制作](ch02-plugin.md) ｜ [返回目录](../README.md) ｜ 下一章：[第四章 · 写一个运行库](ch04-lib.md)
