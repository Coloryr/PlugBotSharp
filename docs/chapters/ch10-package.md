# 第十章：打包与发布

这一章讲组件怎么打成能安装的包、怎么发布和更新。打包本身由 `build/ComponentPackage.targets` 完成，你只需要把 `Meta.cs` 写对。

## 1. 打包产物

```bash
dotnet build Plugin/EchoPlugin/EchoPlugin.csproj
```

产物：`Plugin/EchoPlugin/bin/Debug/net10.0/EchoPlugin.zip`，内容是：

```
Meta.json            构建时由 Meta.cs 生成的清单
EchoPlugin.dll       清单 Dlls 里列出的程序集
EchoPlugin.pdb       Debug 构建的符号文件，跟着 dll 一起打包
runtimes/...         引用了带 native 的 NuGet 包时才会出现
```

- 符号文件：Debug 构建会把 `.pdb` 自动打进包，调试断点靠它；Release 构建默认没有，想保留就把 `<DebugType>embedded</DebugType>` 加到 csproj，pdb 会嵌进 dll
- zip 里**不要**放依赖组件的 dll、配置文件、日志
- 清单里的 `Dlls` 决定哪些文件进包，多一个少一个都会出问题

## 2. 清单检查表

| 字段 | 检查点 |
| --- | --- |
| `Id` | 小写、无空格，和目录名一致，发布后别改（改了 http 路径和事件名前缀都变） |
| `Name` / `Description` | 面板上显示的名字和说明 |
| `Version` | 语义化版本，依赖检查会比对 |
| `Type` | `Plugin` / `Proxy` / `Lib`，决定了能不能建实例 |
| `Dlls` | 只写自己的程序集 |
| `Deps` | 依赖的组件 / NuGet 包，版本写范围别写死 |

## 3. 发布到面板

三种安装方式：

1. 面板「组件」页上传 zip
2. 填本地 zip 或目录路径
3. 直接解压到运行目录的 `Components/{组件id}/`

装好之后：

- 按历史启用记录自动加载；运行库只加载
- 代理组件启用后才能建实例
- 改配置在面板里填，保存自动重载

## 4. 更新和回滚

更新就是重新打包、重新上传：

```bash
# 改版本号
# Meta.cs: Version = "1.1.0"

dotnet build Plugin/EchoPlugin/EchoPlugin.csproj
# 面板上传新的 EchoPlugin.zip
```

- 覆盖安装后组件会重新加载，配置目录 `Configs/{组件id}` 不动
- 回滚就是传回上一版 zip；配置如果有不兼容变更，要自己在 `Init` / `Reload` 里兜住
- 面板「卸载」只是运行时卸载，重启还会加载；「删除」才会删目录

## 5. 发布前自查

| 项 | 确认 |
| --- | --- |
| 干净环境能装上 | 在没装过依赖的机器上装一遍，看 `blocked` 提示 |
| 依赖声明完整 | `Deps` 里有 `Nuget` 的包、有依赖的组件 |
| native 库能加载 | 引用了 SkiaSharp 这类包时，确认 `runtimes/` 跟着进了包 |
| 配置默认值合理 | 删掉 `Configs/{组件id}` 后还能启动 |
| 日志不刷屏 | 循环里别打 `Info`，用 `Debug` |
| 卸载干净 | `Disable` / `Unload` 里停掉线程、定时器、服务器 |

## 6. 目录和版本约定

```
Plugin/EchoPlugin/          插件
Proxy/EchoProxy/            代理
Lib/EchoLib/                运行库
docs/                       文档
build/                      打包工具
```

- 组件工程名用 `EchoPlugin` 这种 PascalCase，程序集名和目录名一致
- `Meta.cs` 放在工程根目录，`Id` 用小写
- 版本号跟着 `Meta.cs` 走，发布说明写清改了什么
- 多组件仓库里，组件之间只通过 `Deps` 依赖，不要 `ProjectReference` 到别人的实现里

## 7. 连续集成（可选）

```bash
dotnet restore
dotnet build PlugBotSharp.slnx -c Release
dotnet test PlugBotSharp.Tests
# 组件包在各自 bin/Release/net10.0/*.zip
```

- 组件包是自包含的 zip，直接把 `*.zip` 当发布产物
- 版本号建议在 `Meta.cs` 里统一维护，或者用 `Directory.Build.props` 集中管
- 发布前跑一遍测试，仓库里的测试不依赖真实组件

---

上一章：[第九章 · 依赖库和运行库](ch09-deps.md) ｜ [返回目录](../README.md) ｜ 下一章：[第十一章 · ComponentApi 进阶](ch11-componentapi.md)
