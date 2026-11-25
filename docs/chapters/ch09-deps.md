# 第九章：依赖库和运行库

组件有三种依赖：别的组件、运行库（其实也是组件）、NuGet 包。这一章讲它们怎么声明、怎么加载、踩坑点在哪。

## 1. 组件依赖

在 `Meta.cs` 的 `Deps` 里声明：

```csharp
Deps =
[
    new DepInfoObj { Type = DepType.Component, Id = "qqlib", Version = ">=1.0.0" },
    new DepInfoObj { Type = DepType.Component, Id = "demolib", Version = ">=1.0.0" }
]
```

- 版本表达式：`>=1.0.0`、`>1.0.0`、`<=1.0.0`、`<1.0.0`、`=1.0.0`（或 `==1.0.0`）、逗号组合；裸版本号按 `>=` 处理
- 宿主按拓扑序加载：被依赖的先加载，依赖它的后加载
- 卸载反过来：先卸依赖它的组件，再卸它自己
- 依赖不满足（找不到、版本不够）时组件被标记成 `blocked`，面板显示「依赖缺失」，不能启用

## 2. 运行库

运行库就是 `Type = Lib` 的组件，作用是**提供类型**：

| 运行库 | 提供什么 |
| --- | --- |
| `qqlib`（`PlugBotSharp.QQLib`） | QQ 消息体、键盘、卡片、`QQMessage` 转换 |
| `onebotlib`（`PlugBotSharp.OneBotLib`） | OneBot 11/12 段模型、`OneBotSegment` |
| `demolib`（`DemoLib`） | 示例运行库，演示 Lib 组件的写法 |

用运行时库的两条规矩：

- 工程里 `ProjectReference` 引一下，编译期用它的类型
- `Meta.cs` 里把对方写进 `Deps`，**不要把它的 dll 抄进自己的 `Dlls`**

抄 dll 的后果：每个组件各加载一份同名程序集，类型标识不同，`is` 判断失败、序列化对不上，而且卸载回收不掉。

## 3. NuGet 包

工程里正常引用，同时在 `Deps` 里声明，宿主会把包装到运行目录的 `Nugets/{包id}/{版本}`：

```xml
<ItemGroup>
  <PackageReference Include="SkiaSharp" Version="4.157.0" />
</ItemGroup>
```

```csharp
Deps =
[
    new DepInfoObj { Type = DepType.Nuget, Id = "SkiaSharp", Version = "4.157.0" }
]
```

装包时宿主会：

- 读包里的 `.nuspec`，按目标框架和平台挑一组依赖**递归安装**子依赖（最多 8 层，去重）
- 版本范围（`[1.0.0,2.0.0)` 这类）会去 NuGet 拉版本列表，取满足条件的最低版本
- `lib/{tfm}` 挑最合适的托管程序集，优先不限平台的 `net10.0`
- `runtimes/{rid}/native` 全解出来，按 RID 选当前平台，P/Invoke 时按文件名 / 加 `lib` 前缀找
- 只有 `.nuspec` 的元包算安装成功

细节见 [package.md](../reference/package.md#nuget-依赖)。

## 4. 依赖的加载顺序

```
demolib (Lib) ──▶ qqlib (Lib) ──▶ qqproxy (Proxy) ──▶ demo (Plugin)
                                         ▲
                                    Echoproxy 依赖 qqlib
```

- 启动时先把所有 Lib 加载完，再按依赖关系加载插件 / 代理
- 组件被禁用不影响依赖它的组件加载，但实例可能起不来
- 运行库被卸载时，依赖它的组件会先被卸载

## 5. 常见坑

| 现象 | 原因 |
| --- | --- |
| `FileNotFoundException` / 类型转换失败 | 把别人的 dll 打进了自己的包，加载了两份 |
| `blocked`，启用不了 | `Deps` 里的 id 写错，或者对方还没装 |
| 依赖版本对不上 | 版本表达式写太死，或者对方升级了不兼容版本 |
| native 库找不到 | 包没声明进 `Deps`（宿主要靠它下载），或者平台不在包里 |
| 两个包要同一个 dll 的不同版本 | 宿主按版本留新的，老包可能行为不一致，尽量对齐版本 |
| 改了运行库但组件没变 | 组件要重载，否则还在用旧程序集 |

## 6. 该不该拆运行库

- 只有**一个**插件要用的类型：直接放插件里，别拆
- **多个**组件要共用，或者想给第三方插件提供扩展点：拆成运行库
- 运行库一旦发布，公共类型改名 / 改签名就是破坏性变更，依赖它的组件都得重编

---

上一章：[第八章 · 远程调试](ch08-debug.md) ｜ [返回目录](../README.md) ｜ 下一章：[第十章 · 打包与发布](ch10-package.md)
