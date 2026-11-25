# 第十二章：RegisterHttpPath 与代理配合

前十一章的组件都是被动等消息。这一章给组件开一扇对外的门：注册 http 接口让外部系统调进来，或者用 `StartServer` 自己监听端口；再配合代理，把结果发到平台上。

## 1. 路径规则

`register.RegisterHttpPath(handler)` 注册的路径由宿主决定，组件不用自己拼：

| 谁注册 | 路径 |
| --- | --- |
| 插件 / 代理 | `/api/{组件id}` |
| 实例 | `/api/{组件id}/{实例uuid}` |

- 同一个路径重复注册会覆盖，后注册的生效
- 路径是**公开的**，不需要登录，谁都能访问
- 组件禁用 / 卸载时，它注册的接口会自动移除
- 端口跟面板一致（默认 `127.0.0.1:10000`），要对外就配证书走 https 或加反向代理

## 2. 注册和取消

只能在 `RegisterEventHook` 阶段注册：

```csharp
public void RegisterEventHook(IRegister register)
{
    register.RegisterHttpPath(HandleAsync);      // /api/echo
}

private static async Task HandleAsync(HttpContext ctx)
{
    ctx.Response.ContentType = "application/json";

    await ctx.Response.WriteAsync("""{"ok":true}""");
}
```

想提前关掉接口（但不禁用组件）：

```csharp
ComponentApi.UnregisterHttpPath(plugin);     // 插件
ComponentApi.UnregisterHttpPath(proxy);      // 代理
ComponentApi.UnregisterHttpPath(instance);   // 实例
```

返回值表示是否真的移除了；只有注册者一致才移除，重复调用返回 `false`。

## 3. 处理请求

`HttpContext` 就是 ASP.NET Core 那套，该怎么写就怎么写：

```csharp
private static async Task HandleAsync(HttpContext ctx)
{
    // 查询参数
    var token = ctx.Request.Query["token"].ToString();

    // 请求体
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();

    // 状态码 + json
    ctx.Response.StatusCode = StatusCodes.Status200OK;

    await ctx.Response.WriteAsJsonAsync(new { ok = true, body });
}
```

- 回调里的异常由宿主捕获，记日志并返回 500；响应已经开始时就只记日志
- 别在回调里做长任务，响应会超时；慢活丢后台（`Task.Run` 或消息队列）
- 一次请求一个 `HttpContext`，不要把它存到字段里

## 4. 自己起服务器（StartServer）

`RegisterHttpPath` 挂在宿主的面板上（固定端口）。要自己监听端口、或者需要 WebSocket，用 `ComponentApi.StartServer` 让宿主托管：

```csharp
private ComponentServer? _server;

public bool Enable()
{
    _server ??= ComponentApi.StartServer(this, new ServerOptions
    {
        Host = "0.0.0.0",
        Port = 8081,
        Certificate = "panel",     // Certs 里的证书名，留空走 http
        WebSocket = true
    }, app =>
    {
        app.MapPost("/hook", HandleHookAsync);
        app.Map("/ws", HandleSocketAsync);
    });

    return _server.Start();
}

public bool Disable()
{
    _server?.Stop();

    return true;
}
```

- `owner` 传组件或实例对象，宿主按它登记；组件禁用 / 卸载、实例禁用 / 删除时自动 `StopAll`，不用自己收尾
- `Start()` 同步执行，成功返回 `true`；`Stop()` 可以重复调用
- `configure` 里就是正常的 ASP.NET Core 路由（`app.Map...`）；需要 WebSocket 就把 `WebSocket` 置为 `true`，宿主会帮你 `UseWebSockets()`
- 证书填 `Certs` 目录里的名字，密码按 `Certs.json` 或密码文件自动解析；直接填 pfx 路径也兼容
- 端口被占用、证书找不到时 `Start()` 返回 `false` 并记日志，不会静默退回 http
- 自己起的服务器默认对所有人开放，鉴权、限流要自己加

和 `RegisterHttpPath` 怎么选：

| 场景 | 用哪个 |
| --- | --- |
| 给面板加个接口，路径跟着组件 id 走 | `RegisterHttpPath` |
| 要自己的端口、WebSocket、平台回调地址 | `StartServer` |
| 要 https、自定义域名或反代后端 | `StartServer` |
| 只是内部调用，不需要外部访问 | `RegisterHttpPath` |

```csharp
// 典型：平台要求回调地址固定，就自己监听一个端口
_server = ComponentApi.StartServer(instance, new ServerOptions
{
    Host = "0.0.0.0",
    Port = 9000,
    Certificate = "qq.example.com",
    WebSocket = true
}, app => app.MapPost("/callback", HandleCallbackAsync));

if (!_server.Start())
{
    Logger.Error("回调服务器启动失败，检查端口和证书");

    return false;
}
```

## 5. 和代理配合的三种模式

| 场景 | 谁注册 http | 数据怎么走 |
| --- | --- | --- |
| 平台 webhook | 代理 | 平台 → 代理的 http → `Dispatch` → 插件 → 代理发回平台 |
| 外部系统接入 | 插件 | 外部 → 插件的 http → `SendHandle` → 代理 → 平台 |
| 多账号 | 实例 | `/api/{proxy}/{uuid}` 一个实例一个入口 |

**平台 webhook（代理侧）**：QQ 代理的 `HookType = WebHook` 就是这样，实例注册 `/api/qqproxy/{uuid}`，平台把事件 POST 进来，代理验签、转成 `ProxyMessage`、`Dispatch` 给插件，再把插件回复发回平台。插件完全不用知道 http 的存在。

**外部系统接入（插件侧）**：插件开一个接口给监控 / 工单系统调，收到之后用 `SendHandle` 让代理发到群里。下面就是这个流程的完整例子。

**多账号**：实例级路径天然按 uuid 区分，同一个代理的多个账号互不干扰。

## 6. 完整例子：外部告警发到群里

需求：监控系统 POST 一条告警到插件，插件通过 QQ 代理发到指定群。

```csharp
public class PluginMain : IBasePlugin
{
    private SendHandle? _send;

    public void RegisterEventHook(IRegister register)
    {
        _send = register.RegisterSend();
        register.RegisterHttpPath(HandleAlertAsync);      // /api/alert
    }

    private async Task HandleAlertAsync(HttpContext ctx)
    {
        // 1. 简单鉴权
        if (ctx.Request.Query["token"] != "change-me")
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // 2. 读报文
        AlertObj? alert;

        try
        {
            alert = await JsonSerializer.DeserializeAsync<AlertObj>(ctx.Request.Body);
        }
        catch
        {
            alert = null;
        }

        if (alert is null || string.IsNullOrWhiteSpace(alert.Text))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new { ok = false, message = "报文无效" });
            return;
        }

        // 3. 找一个可用的 QQ 实例
        var instance = ComponentApi.GetInstances()
            .FirstOrDefault(x => x.Proxy == "qqproxy" && x.Enabled);

        if (instance is null)
        {
            await ctx.Response.WriteAsJsonAsync(new { ok = false, message = "没有可用的 QQ 实例" });
            return;
        }

        // 4. 交给代理发出去（会过所有发送拦截器）
        var context = new ProxyMessage
        {
            Id = instance.Proxy,
            Uuid = instance.Uuid,
            SenderType = MessageSenderType.Group,
            Sender = "alert",
            Target = alert.Group,
            Message = new TextMessage("")
        };

        await _send!.SendAsync(context, new TextMessage(alert.Text));

        // 5. 告诉调用方结果
        await ctx.Response.WriteAsJsonAsync(new { ok = true, instance = instance.Uuid });
    }
}

public class AlertObj
{
    public string Group { get; set; } = "";
    public string Text { get; set; } = "";
}
```

调用：

```bash
curl -X POST "http://127.0.0.1:10000/api/alert?token=change-me" ^
  -H "Content-Type: application/json" ^
  -d "{\"group\":\"123456\",\"text\":\"CPU 90%\"}"
```

这段代码串起了三样东西：`RegisterHttpPath`（对外）、`ComponentApi`（找实例）、`SendHandle`（走代理发送）。

## 7. 安全

接口是公开的，以下几点别省：

| 项 | 做法 |
| --- | --- |
| 鉴权 | 查询参数或 `Authorization` 头带 token，和配置里的值比对 |
| 校验 | 报文结构、必填字段、长度上限 |
| 限流 | 简单计数（每秒最多 N 次），防止被刷 |
| 日志 | 记调用方 ip、路径、结果，别记敏感内容 |
| 暴露面 | 只在内网 / 反代后面开放，别直接把面板端口挂公网 |
| 幂等 | 重试场景下同一请求会到两次，按业务加去重 |

## 8. 注意

- 注册的接口跟着组件走：禁用、卸载、删除都会自动摘掉
- 回调是同步执行的，一个慢请求会占一个线程，长任务丢后台
- 响应已经开始写之后就别再改状态码，宿主只会记日志
- 组件之间不要互相 `ProjectReference`，要协作就用事件（[第五章](ch05-events.md)）和这些接口
- 接口路径依赖组件 id，改了 id 等于改了地址，外部调用方要一起改

## 9. 小结

- 对外接口用 `RegisterHttpPath`，路径 `/api/{组件id}` 或 `/api/{组件id}/{uuid}`
- 代理收平台回调、插件接外部系统，两条路最后都汇到 `BaseMessage`
- 插件侧要发消息：`ComponentApi.GetInstances` 找实例，`SendHandle.SendAsync` 发出去
- 安全靠 token + 校验 + 限流，别把内部接口暴露到公网

---

上一章：[第十一章 · ComponentApi 进阶](ch11-componentapi.md) ｜ [返回目录](../README.md) ｜ 下一章：无
