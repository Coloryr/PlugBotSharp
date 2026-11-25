# 第七章：自定义数据（状态）

配置是「面板改、组件读」，状态反过来：**组件写、面板看**。用来把运行时数据摆到面板上，相当于给自己做监控。

## 1. 注册

在 `RegisterEventHook` 里拿句柄，之后随时更新：

```csharp
private StateValueHandle? _replyState;
private StateKvHandle? _commandState;

public void RegisterEventHook(IRegister register)
{
    // 单值：面板显示成「回复条数: 12」
    _replyState = register.RegisterState("replies", "回复条数", "累计回复的消息条数");

    // 键值：面板显示成可展开的表格
    _commandState = register.RegisterKvState("commands", "命令次数", "各命令被调用的次数");
}
```

三个参数：`name` 是键（接口里用它）、`label` 是面板上显示的名字（不填就用 `name`）、`description` 是解释（鼠标停在卡片上看得到）。

注册时就记下了名字和描述，值还没上报时面板上也会出现，单值显示 `-`、键值显示 `0 项`。

## 2. 更新

```csharp
// 单值：基础类型直接传，面板按原类型显示
_replyState?.Set(12);
_replyState?.Set(true);
_replyState?.Set(0.5);
_replyState?.Set(9000000000L);

// 键值：增、删、清空
_commandState?.Set("/echo", 3);
_commandState?.Set("/who", 1);
_commandState?.Remove("/who");
_commandState?.Clear();
```

`Set` 有 `string` / `bool` / `char` / `byte` / `sbyte` / `short` / `ushort` / `int` / `uint` / `long` / `ulong` / `float` / `double` / `decimal` 重载，数字不会变成字符串。

## 3. 面板上长什么样

- 组件页 / 实例页：每个对象名字下面一排小标签，`名字: 值`
- 概览页：按组件 / 实例自动分组，**一项状态一张卡片**，和顶部「运行时长」同一套样式；键值状态在卡片里点「查看」展开
- 键值状态是只读的：面板只能看，不能改

## 4. 用什么类型

| 需求 | 用哪个 |
| --- | --- |
| 一个计数、开关、当前状态 | `RegisterState`（单值） |
| 每个群 / 用户的数字，或者一组键值 | `RegisterKvState`（键值） |
| 需要面板能改 | 不是状态，是[配置](./ch06-config.md) |

键值状态不适合放大文本或敏感数据：值会原样进面板接口。

## 5. 更新时机

- 状态变化时更新就行，别在循环里每毫秒 `Set` 一次
- 高频场景（比如每秒几十次的计数）可以只留最新值，`Set` 是覆盖写
- 句柄可以一直留着，注册阶段结束后照样能 `Set` / `Remove`；组件卸载时宿主会清掉它上报的全部状态

## 6. 自动落盘

状态由**宿主自动保存**，组件不用自己写文件：

- 组件存在 `Configs/{组件id}/states.json`，实例存在 `Instances/{uuid}/states.json`
- 默认每 2 秒把改过的状态写一次盘；组件卸载、实例删除、进程正常退出时也会立刻刷一次
- 间隔可以调：运行目录 `Config.json` 里的 `StateSaveInterval`（毫秒，最小 `1000`，默认 `2000`），改完重启生效
- 重新加载时，`RegisterState` / `RegisterKvState` 会把上次落盘的值读回来，句柄上的 `Get()` 能拿到：

```csharp
_receiveState = register.RegisterState("recv", "收到条数", "累计收到的条数");

// 重启后接着上次的数
_received = _receiveState.Get() is { } value ? Convert.ToInt64(value) : 0;
_receiveState.Set(_received);
```

- 键值状态同理：`Get()` 返回 `IReadOnlyDictionary<string, object?>`
- 落盘的值按原类型还原：整数是 `long`、小数是 `double`、开关是 `bool`、文本是 `string`
- 运行时标志（比如“当前是否已连接”）也会被存下来，重新注册后自己 `Set` 一次当前真实值覆盖掉就行
- 想清掉累计值，删掉对应的 `states.json` 再重载组件 / 实例

## 7. 面板接口里的样子

`GET /api/admin/components` 和 `GET /api/admin/instances` 的 `states` 字段：

```json
{
  "replies": {
    "label": "回复条数",
    "description": "累计回复的消息条数",
    "kind": "value",
    "value": 12
  },
  "commands": {
    "label": "命令次数",
    "description": "各命令被调用的次数",
    "kind": "kv",
    "value": { "/echo": 3 }
  }
}
```

概览页用的 `GET /api/admin/overview` 里，`componentItems` / `instanceItems` 每项也带同样的 `states`。

## 8. 例子

- [DemoPlugin](../../Plugin/DemoPlugin)：`pings` / `events` / `replies` / `blocked` 单值，`commands` 键值
- [QQ 代理](../../Proxy/PlugBotSharp.QQProxy)：`quota` 键值，记每天各群已发的主动消息条数

## 9. 注意

- 状态名在同一个组件里唯一，同名会覆盖
- 值只能是基础类型 / 键值字典，不要挂自己的对象（面板要 JSON 序列化）
- 不要用状态存需要跨重启保留的数据，重启就没了，用配置文件

---

上一章：[第六章 · 配置文件](ch06-config.md) ｜ [返回目录](../README.md) ｜ 下一章：[第八章 · 远程调试](ch08-debug.md)
