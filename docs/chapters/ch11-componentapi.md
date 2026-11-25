# 第十一章：ComponentApi 进阶

前几章都是「写自己的组件」。这一章讲反过来：**组件去管别的组件**。`ComponentApi` 是宿主暴露给组件的静态入口，随时都能调，不用等注册阶段。

## 1. 能查什么

| 方法 | 返回 | 说明 |
| --- | --- | --- |
| `GetComponents()` | `List<ComponentInfo>` | 全部组件，含磁盘上还没加载的 |
| `GetComponentState(id)` | `string` | `enabled` / `disabled` / `loaded` / `unloaded` / `blocked` |
| `GetComponentConfig(id)` | `Dictionary<string, ConfigMap>?` | 组件的配置项，没提供返回 `null` |
| `GetInstances()` | `List<InstanceInfo>` | 全部实例，含磁盘上还没加载的 |
| `GetInstanceConfig(uuid)` | `Dictionary<string, ConfigMap>?` | 实例的配置项 |

`ComponentInfo` 的字段：`Id` / `Name` / `Version` / `Type` / `Description` / `Folder` / `Loaded` / `Enabled` / `Blocked` / `State`。
`InstanceInfo` 的字段：`Uuid` / `Proxy` / `Name` / `Folder` / `Loaded` / `Enabled`。

## 2. 能做什么

| 方法 | 说明 |
| --- | --- |
| `StartComponent(id)` | 没加载先加载，然后启用；运行库只加载 |
| `DisableComponent(id)` | 只停用，不卸载；运行库返回 `false` |
| `ReloadComponent(id)` | 重启：停用 → 重读配置 → 按原来的状态恢复 |
| `UpdateComponentConfig(id, map)` | 把新配置交给组件的 `UpdateMap` |
| `EnableInstance(uuid)` | 启用实例，没加载会先从磁盘加载 |
| `DisableInstance(uuid)` | 禁用实例 |
| `ReloadInstance(uuid)` | 重启实例：停用 → 重读配置 → 恢复原状 |
| `UpdateInstanceConfig(uuid, map)` | 把新配置交给实例的 `UpdateMap` |

- 返回 `bool` 的方法失败时返回 `false`，原因写在宿主日志里，不抛异常
- id / uuid 不存在也返回 `false`，只有 id 为空才抛 `ArgumentException`
- 启用 / 禁用 / 重启都不会卸载程序集，所以组件在自己的回调里操作自己也是安全的

## 3. 场景一：等依赖就绪

插件启动时依赖的代理可能还没启用。别死循环，用 `Tick` 或者定时器轮询：

```csharp
private DateTime _lastCheck;
private bool _ready;

public void Tick()      // 插件没有 Tick，代理 / 实例才有；插件用 Timer 也行
{
    if (_ready || (DateTime.Now - _lastCheck).TotalSeconds < 5)
    {
        return;
    }

    _lastCheck = DateTime.Now;

    if (ComponentApi.GetComponentState("qqproxy") == "enabled")
    {
        _ready = true;
        Logger.Info("QQ 代理就绪");
    }
}
```

更省事的写法是查实例：

```csharp
var instance = ComponentApi.GetInstances().FirstOrDefault(x => x.Proxy == "qqproxy" && x.Enabled);

if (instance is not null)
{
    // 有可用的 QQ 实例
}
```

## 4. 场景二：确保代理和实例可用

比如一个管理插件，重载后把所有代理实例拉起来：

```csharp
foreach (var item in ComponentApi.GetInstances())
{
    if (!item.Enabled)
    {
        ComponentApi.EnableInstance(item.Uuid);
    }
}
```

`EnableInstance` 对磁盘上还没加载的实例也有效，会先从 `Instances/{uuid}` 读出来再启用。

## 5. 场景三：跨组件读配置

比如读 QQ 代理的实例配置，拿到群发限制：

```csharp
var config = ComponentApi.GetInstanceConfig(uuid);

if (config is not null && config.TryGetValue("PassiveLimit", out var item) && item is ConfigIntMap map)
{
    Logger.Info($"这个实例的被动回复上限是 {map.Value}");
}
```

注意：配置项是**当场拼的快照**，改它不会写回去，要改就 `UpdateComponentConfig` / `UpdateInstanceConfig`。

## 6. 场景四：健康检查接口

组件注册一个 http 路径，返回组件 / 实例状态，远程 `curl` 就能看：

```csharp
register.RegisterHttpPath(async ctx =>
{
    var components = ComponentApi.GetComponents()
        .Select(x => new { x.Id, x.State, x.Enabled });

    var instances = ComponentApi.GetInstances()
        .Select(x => new { x.Uuid, x.Proxy, x.Enabled });

    await ctx.Response.WriteAsJsonAsync(new { components, instances });
});
```

```bash
curl http://127.0.0.1:10000/api/echo
```

- 路径 `/api/{组件id}` 是公开的，别把 token、密码放进去
- 这个接口适合运维巡检，面板上也能看到同样的数据

## 7. 场景五：给自己做面板

插件可以拿 `ComponentApi` 做一个「一键操作」入口：重启所有实例、禁用某个代理、批量改配置。

```csharp
register.RegisterHttpPath(async ctx =>
{
    var action = ctx.Request.Query["action"];

    switch (action)
    {
        case "reload-all":
            foreach (var item in ComponentApi.GetInstances())
            {
                ComponentApi.ReloadInstance(item.Uuid);
            }
            break;

        case "stop-qq":
            ComponentApi.DisableComponent("qqproxy");
            break;
    }

    await ctx.Response.WriteAsync("""{"ok":true}""");
});
```

## 8. 注意

| 事项 | 说明 |
| --- | --- |
| 权限 | `ComponentApi` 不区分调用者，任何组件都能停别人的组件，别在插件里做危险操作 |
| 频率 | `GetComponents` / `GetInstances` 会扫磁盘目录，别在循环里每毫秒调 |
| 返回值 | 失败只返回 `false`，要原因就看宿主日志 |
| 线程 | 这些方法都是同步的，别在 UI 线程或锁里长时间占着 |
| 自启 | `StartComponent` 会真的启用组件，包括你自己，重复调用是幂等的 |

## 9. 小结

- 查：`GetComponents` / `GetInstances` / `GetComponentState` / 两种 `Get*Config`
- 做：`StartComponent` / `DisableComponent` / `ReloadComponent` / `UpdateComponentConfig`
- 实例：`EnableInstance` / `DisableInstance` / `ReloadInstance` / `UpdateInstanceConfig`
- 组件之间不该互相 `ProjectReference`，需要协作就通过这些接口 + 事件，保持可热插拔

接口的完整签名见[接口与 API 参考](../reference/api.md#调用宿主接口)。

---

上一章：[第十章 · 打包与发布](ch10-package.md) ｜ [返回目录](../README.md) ｜ 下一章：[第十二章 · RegisterHttpPath 与代理配合](ch12-http.md)
