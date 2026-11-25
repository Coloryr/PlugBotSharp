# 第二章：完整插件制作过程

这一章从空目录开始，做一个能装进面板、能解析消息、能发消息的插件 `EchoPlugin`（id `echo`）。功能不复杂，但把插件该有的东西都走一遍：

- 建工程、写清单、打包成 zip
- 解析收到的消息（文本、图片、@、引用、列表）
- 回复文本、图片、多条消息、引用、平台专属消息
- 面板配置、面板状态
- 安装、启用、调试

仓库里的 [DemoPlugin](../../Plugin/DemoPlugin) 就是按这个流程写的，可以对照着看。

## 1. 建工程

```
Plugin/EchoPlugin/
  EchoPlugin.csproj    工程文件
  Meta.cs              组件清单
  PluginMain.cs        插件本体
```

`EchoPlugin.csproj` 引用宿主核心和打包工具，最后 Import 打包目标：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Core\PlugBotSharp.Core\PlugBotSharp.Core.csproj" />
    <ProjectReference Include="..\..\build\MetaGen\MetaGen.csproj" ReferenceOutputAssembly="false" />
  </ItemGroup>

  <Import Project="..\..\build\ComponentPackage.targets" />

</Project>
```

- 只引用 `PlugBotSharp.Core`，事件、消息模型、注册器都在里面
- `MetaGen` 只参与构建（`ReferenceOutputAssembly="false"`），用来生成 `Meta.json`
- 最后那行 Import 让构建结束后自动把清单和 dll 打成 zip

## 2. 写清单 Meta.cs

```csharp
using PlugBotSharp.Core.Configs;

public static class Meta
{
    public static MetaObj Info = new()
    {
        Name = "EchoPlugin",
        Id = "echo",
        Version = "1.0.0",
        Description = "示例：完整插件制作",
        Type = ComponentType.Plugin,
        Dlls = ["EchoPlugin.dll"],
        Deps = []
    };
}
```

- `Id` 是目录名、`/api/{id}` 的前缀、事件名 `[echo]xxx` 的前缀，定了别乱改
- `Dlls` 只写自己的程序集；引用了别的组件（比如 `qqlib`）就把对方写进 `Deps`，**不要**把它的 dll 抄进 `Dlls`
- `Type = Plugin`，插件没有实例，装上就能启用

## 3. 插件骨架

```csharp
using PlugBotSharp.Core;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.Core.Plugin;

namespace EchoPlugin;

public class PluginMain : IBasePlugin
{
    public bool Init(string path) => true;
    public bool Load() => true;
    public void Reload() { }
    public bool Enable() => true;
    public bool Disable() => true;
    public bool Unload() => true;

    public Dictionary<string, ConfigMap> ConfigMap() => new();

    public void UpdateMap(Dictionary<string, ConfigMap> data) { }

    public void RegisterEventHook(IRegister register)
    {
        register.SubscribeMessage(HandleMessage);
    }

    /// <summary>
    /// 收到消息后决定回什么
    /// </summary>
    private List<BaseMessage> HandleMessage(ProxyMessage message)
    {
        return [];
    }
}
```

`Init` 拿到的是 `Configs/echo`，配置文件就写在这个目录里。完整生命周期见 [第一章](../README.md#生命周期)。

## 4. 解析消息

每次有人发消息，宿主会把代理转好的 `ProxyMessage` 交给 `HandleMessage`：

| 字段 | 含义 |
| --- | --- |
| `Id` / `Uuid` | 哪个代理、哪个实例发来的 |
| `SenderType` | `User` 私聊 / `Group` 群 / `Guild` 频道 |
| `Sender` | 发送者 id |
| `Target` | 回复目标，群里是群号，私聊为空 |
| `ReplyId` | 平台消息 id，引用回复时用 |
| `Message` | 统一消息 `BaseMessage`，真正的正文 |

`BaseMessage` 有很多子类型，按需判断：

| 收到的类型 | 判断写法 | 能取到什么 |
| --- | --- | --- |
| 文本 | `is TextMessage text` | `text.Text` |
| 多条合一条 | `is ListMessage list` | `list.Messages`，逐个递归 |
| 网络图片 | `is ImageUrlMessage image` | `image.Url` |
| 本地图片 | `is ImageFileMessage image` | `image.File` |
| 图片数据 | `is ImageMessage image` | `image.Image`（Stream） |
| @ | `is MentionMessage mention` | 被 @ 的人 |
| 引用 | `is ReplyMessage reply` | `reply.MessageId` |
| 表情 | `is FaceMessage face` | 表情 id |
| 平台专属 | `is RawMessage raw` | `raw.Name` + `raw.Payload` |

把消息摊成一段文本，方便解析命令：

```csharp
/// <summary>
/// 把一条消息摊成文本，列表就递归拼起来
/// </summary>
private static string GetText(BaseMessage message)
{
    return message switch
    {
        TextMessage text => text.Text,
        MarkdownMessage markdown => markdown.Content,
        ListMessage list => string.Join(" ", list.Messages.Select(GetText)),
        MentionMessage => " ",
        _ => ""
    };
}
```

然后就是普通的命令解析，和写控制台程序一样：

```csharp
private List<BaseMessage> HandleMessage(ProxyMessage message)
{
    var text = GetText(message.Message).Trim();

    if (text.Length == 0)
    {
        return [];             // 没文本就不管
    }

    // "/echo 你好" -> 命令 /echo，参数 你好
    var index = text.IndexOf(' ');
    var command = (index < 0 ? text : text[..index]).ToLowerInvariant();
    var arg = index < 0 ? "" : text[(index + 1)..].Trim();

    return command switch
    {
        "/echo" => arg.Length == 0 ? [new TextMessage("用法：/echo 内容")] : [new TextMessage(arg)],
        "/who" => [new TextMessage($"发送者 {message.Sender}，会话 {message.SenderType}，目标 {(message.Target.Length == 0 ? "私聊" : message.Target)}")],
        "/quote" => BuildQuote(message),
        "/pic" => [new ImageUrlMessage("https://example.com/cat.png", ImageType.Png)],
        "/list" => [new ListMessage(new TextMessage("第一条"), new TextMessage("第二条"))],
        _ => KeywordReply(text)
    };
}
```

- 命令统一 `Trim()` + `ToLowerInvariant()`，别假定客户端发来的格式
- `[..index]` 这种区间写法比 `Substring` 清爽，参数里带空格也没问题
- 不是命令的消息走关键词回复（下一步接配置）

## 5. 发送消息

回复就是**返回 `List<BaseMessage>`**，宿主会按 `message.Uuid` 交回给发消息的那个实例，代理再翻译成平台格式。

```csharp
// 一条文本
return [new TextMessage("收到")];

// 网络图片
return [new ImageUrlMessage("https://example.com/a.png", ImageType.Png)];

// 本地图片，代理负责上传
return [new ImageFileMessage("D:\\pics\\a.png", ImageType.Png)];

// 多条消息合成一组发
return [new ListMessage(new TextMessage("第一条"), new TextMessage("第二条"))];

// 引用回复
return [new ListMessage(new ReplyMessage(message.ReplyId!), new TextMessage("引用了上面那条"))];

// 平台专属（QQ 的键盘、卡片、ark）
return [new RawMessage(QQMessage.SendName, payload)];
```

```csharp
/// <summary>
/// 引用回复，没有消息 id 就退回普通回复
/// </summary>
private static List<BaseMessage> BuildQuote(ProxyMessage message)
{
    if (string.IsNullOrEmpty(message.ReplyId))
    {
        return [new TextMessage("这条消息没有 id，引用不了")];
    }

    return [new ListMessage(new ReplyMessage(message.ReplyId), new TextMessage("引用回复"))];
}
```

几个细节：

- 图片：`ImageUrlMessage` 给地址，`ImageFileMessage` 给本地路径，`ImageMessage` 给 Stream；QQ 侧代理会先上传换成 `file_info`，OneBot 侧直接转 `base64` 或文件
- 代理按自己的能力处理 `ListMessage`：能一次发的就一次发，不能的就拆成多条
- 返回空列表 `[]` 就是"不回"，别返回 `null`
- 平台认不出来的消息会**直接丢掉**，不会报错，所以插件侧不用 try/catch 消息类型

要**主动发**（不等消息）就留一个 `SendHandle`：

```csharp
private SendHandle? _send;

public void RegisterEventHook(IRegister register)
{
    register.SubscribeMessage(HandleMessage);

    _send = register.RegisterSend();     // 注册阶段拿到
}

// 任意时刻
public async Task NotifyAsync(Guid uuid)
{
    var context = new ProxyMessage
    {
        Id = "qq",                        // 目标代理组件 id
        Uuid = uuid,                      // 目标实例
        SenderType = MessageSenderType.Group,
        Sender = "system",
        Target = "group",
        Message = new TextMessage("")     // 只是上下文
    };

    await _send!.SendAsync(context, new TextMessage("主动发出来的消息"));
}
```

- 主动发送同样要过所有插件的发送拦截器
- 不知道实例 uuid 时用 `ComponentApi.GetInstances()` 查
- 发送失败只记日志，不会抛到调用方

## 6. 面板配置

插件可以把自己的配置项声明出来，面板照着生成表单。这里做一个"关键词 - 回复"的映射：

```csharp
private string _path = "";
private ConfigObj _config = new();

/// <summary>
/// 落盘的配置
/// </summary>
private sealed class ConfigObj
{
    public Dictionary<string, string> Keywords { get; set; } = new();
}

public bool Init(string path)
{
    _path = path;          // Configs/echo

    return true;
}

public bool Load()
{
    ReadConfig();

    return true;
}

private void ReadConfig()
{
    try
    {
        var file = Path.Combine(_path, "config.json");

        if (File.Exists(file))
        {
            _config = JsonSerializer.Deserialize<ConfigObj>(File.ReadAllText(file)) ?? new ConfigObj();
        }
    }
    catch
    {
        _config = new ConfigObj();
    }
}

private void SaveConfig()
{
    File.WriteAllText(Path.Combine(_path, "config.json"),
        JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));
}
```

声明配置项，`Key` 是面板上显示的字段名：

```csharp
public Dictionary<string, ConfigMap> ConfigMap() => new()
{
    ["Keywords"] = new ConfigKvMap
    {
        Key = "Keywords",
        Value = new Dictionary<string, string>(_config.Keywords),
        Default = [],
        Description = "关键词 - 回复内容，文本里出现关键词就回对应的内容"
    }
};

public void UpdateMap(Dictionary<string, ConfigMap> data)
{
    if (data.TryGetValue("Keywords", out var item) && item is ConfigKvMap map)
    {
        _config.Keywords = new Dictionary<string, string>(map.Value);
    }

    SaveConfig();
}
```

用起来就是一个普通的字典查找：

```csharp
private List<BaseMessage> KeywordReply(string text)
{
    foreach (var (key, value) in _config.Keywords)
    {
        if (text.Contains(key, StringComparison.OrdinalIgnoreCase))
        {
            return [new TextMessage(value)];
        }
    }

    return [];
}
```

- `ConfigMap()` 每次面板打开设置都会调，返回的是当场拼的快照，改了 `_config` 记得让下次返回新值
- `UpdateMap` 收到面板提交的同一批配置项，**自己落盘**，宿主不会替你存
- 保存后面板会调 `Reload`，不需要在这里立刻生效也行，但落盘一定要做

## 7. 面板状态

状态是给面板看的运行时数据，注册阶段拿句柄，之后随时更新：

```csharp
private StateValueHandle? _replyState;
private StateKvHandle? _commandState;

public void RegisterEventHook(IRegister register)
{
    register.SubscribeMessage(HandleMessage);

    _replyState = register.RegisterState("replies", "回复条数", "累计回复的消息条数");
    _commandState = register.RegisterKvState("commands", "命令次数", "各命令被调用的次数");
}
```

在 `HandleMessage` 返回前更新：

```csharp
var replies = command switch { ... };

if (replies.Count > 0)
{
    _replyCount += replies.Count;
    _replyState?.Set(_replyCount);

    var count = _commands.TryGetValue(command, out var old) ? old + 1 : 1;

    _commands[command] = count;
    _commandState?.Set(command, count);
}

return replies;
```

- 单值状态用 `Set(值)`，键值状态用 `Set(键, 值)` / `Remove(键)` / `Clear()`
- 值可以是数字、布尔、字符串，面板按原类型显示
- 面板的「概览」页会按组件自动分组，一项状态一张卡片，不用自己做界面

## 8. 打包和安装

```bash
dotnet build Plugin/EchoPlugin/EchoPlugin.csproj
```

构建会把清单和 dll 打成 `Plugin/EchoPlugin/bin/Debug/net10.0/EchoPlugin.zip`，里面就是：

```
Meta.json
EchoPlugin.dll
```

如果引用了带 native 库的 NuGet 包，包里的 `runtimes/` 也会跟着进 zip。

装进面板的三种方式：

1. 面板「组件」页上传 zip
2. 填本地 zip 或目录路径
3. 直接解压到运行目录的 `Components/echo/`

装好之后：

- 组件页会显示「已加载」，点「启用」变成「运行中」
- 禁用 / 卸载 / 重载都不用重启进程
- 保存设置后面板自动重载

依赖和版本：

- 引用别的组件：`Deps` 里写 `Component`，宿主按 id 找，找不到就把你标成 `blocked`
- 引用 NuGet 包：`PackageReference` + `Deps` 里写 `Nuget`，宿主会下到 `Nugets/`（子依赖和 native 库也一起装）
- 版本号在 `Meta.cs` 里，依赖检查会比对

调试：

- 日志用 `LogManager.GetLogger("Echo")`，和控制台、`Logs/yyyy-MM-dd.log` 是一份
- 发一条 `/echo hi` 消息，能在日志看到收发就说明链路通了
- 面板「概览」页的自定义状态卡片能直接看 `replies` / `commands`

## 9. 常见问题

| 现象 | 原因 |
| --- | --- |
| 装上但收不到消息 | 组件没启用；或者 `RegisterEventHook` 里没 `SubscribeMessage` |
| 命令没反应 | 大小写、全角斜杠、平台自带前缀，先 `Trim()` + `ToLowerInvariant()` |
| 回复了多条 | 别的插件也返回了消息，宿主会把所有插件的回复拼起来发 |
| 消息发出去被拦掉 | 有插件注册了 `SubscribeSendFilter` 并返回 `true` |
| 配置保存了没生效 | `UpdateMap` 里要落盘，并让下次 `ConfigMap()` 返回新值 |
| 卸载后内存没释放 | 插件里缓存了别的组件的对象或线程没停，只能缓存基础类型 / `JsonNode` / `BaseMessage` |
| 慢操作卡住机器人 | 同步回调里别做耗时操作，换 `SubscribeMessage(async e => ...)` |

完整可运行的版本就是 [DemoPlugin](../../Plugin/DemoPlugin)：同样的骨架，多了键盘、卡片、引用、拦截器、自定义事件这些示例。下一步可以看 [plugin.md](../reference/plugin.md) 里的消息类型清单和 [api.md](../reference/api.md) 的进阶内容。

---

上一章：[第一章 · 快速上手](../README.md) ｜ [返回目录](../README.md) ｜ 下一章：[第三章 · 写一个代理](ch03-proxy.md)
