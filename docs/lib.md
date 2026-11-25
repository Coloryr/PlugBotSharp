# 写运行库

运行库（`Type = Lib`）只提供类型和工具，不处理消息，也不参与启用 / 禁用。宿主加载它之后，别的组件可以直接引用它的类型。

例子：[DemoLib](../Lib/DemoLib)（id `demolib`）。

## 接口

```csharp
public class LibMain : IBaseLib
{
    public bool Init(string path) { return true; }   // Configs/{组件id}
    public bool Load() { return true; }
    public bool Unload() { return true; }
}
```

- 生命周期只有 `Init → Load → Unload`，没有 `Enable` / `Disable`
- `Init` 也只会调一次，配置目录是 `Configs/{组件id}`
- 运行库被卸载前，依赖它的组件会先卸载

## DemoLib

```
Lib/DemoLib/
  DemoLib.csproj    引用 Core 和 MetaGen，导入 ComponentPackage.targets
  Meta.cs           Type = ComponentType.Lib，Dlls = ["DemoLib.dll"]
  LibMain.cs        IBaseLib 的空实现
```

想给别的组件提供类型时，把类型放在这里，别的组件用 `Deps` 声明依赖，**不要**把 `DemoLib.dll` 打进自己的包。

## QQLib 和 OneBotLib

两个真实运行库，代理和插件都用：

| 运行库 | 提供 |
| --- | --- |
| `PlugBotSharp.QQLib` | QQ 消息体模型（`SendGroupMessageObj`、键盘、卡片、ark）、`QQMessage` 的 `BaseMessage` ↔ QQ 转换、`QQJsonContext` |
| `PlugBotSharp.OneBotLib` | OneBot 11 / 12 消息段模型、`OneBotMessage` 的双向转换、平台专属段（`OneBotSegment`） |

插件想发平台专属消息时引用它们，比如：

```csharp
using PlugBotSharp.QQLib;

var obj = new SendGroupMessageObj { MsgType = 2, Markdown = new MessageMarkdownObj { Content = "**hi**" } };
return [new RawMessage(QQMessage.SendName, JsonSerializer.SerializeToNode(obj, QQJsonContext.Default.SendGroupMessageObj)!)];
```

## 注意

- 运行库没有启停，别在里面起线程或服务器，需要的话放到代理 / 插件里
- 运行库改了之后，依赖它的组件要重新加载（面板上重载或重启进程）
- 只放共享类型和纯函数，运行库里的静态状态所有组件共用
