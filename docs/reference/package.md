# 组件清单、依赖和打包

## 组件目录

装好的组件在 `Components/{组件id}/`，里面至少要有一个 `Meta.json`（旧的 `Plugin.json` / `Proxy.json` 已不再识别）：

```json
{
  "Name": "QQProxy",
  "Id": "qq",
  "Version": "1.0.0",
  "Description": "官Q机器人",
  "Type": "Proxy",
  "Dlls": ["PlugBotSharp.QQProxy.dll"],
  "Deps": [
    { "Type": "Component", "Id": "demolib", "Version": ">=1.0.0" },
    { "Type": "Nuget", "Id": "SkiaSharp", "Version": "4.157.0" }
  ]
}
```

| 字段 | 说明 |
| --- | --- |
| `Name` / `Id` | 显示名 / 组件 id，id 也是目录名和 http 路径的一部分 |
| `Version` | 组件版本，依赖检查用 |
| `Type` | `Plugin` / `Proxy` / `Lib` |
| `Dlls` | 真正属于这个组件的程序集，会被打进安装包 |
| `Deps` | 依赖：`Component` 是别的组件，`Nuget` 是 NuGet 包 |

版本表达式支持 `>=1.0.0`、`>1.0.0`、`<=1.0.0`、`<1.0.0`、`=1.0.0` / `==1.0.0`（等于）、逗号组合；裸版本号（`1.0.0`）按最低版本处理，等价于 `>=1.0.0`。

## NuGet 依赖

组件声明的 `Nuget` 依赖会装到运行目录的 `Nugets/{包id}/{版本}`，装好后：

- **子依赖**：读包里的 `.nuspec`，按目标框架和平台挑一组依赖递归安装，最多 8 层，装过的包和版本会去重；子依赖装不上只记警告，不影响组件本身
- **版本范围**：`Deps` 和 nuspec 里都可以写 `[1.0.0,2.0.0)`、`[1.0.0]`、`(,2.0.0]` 这类范围，宿主会去 NuGet 拉版本列表，按 NuGet 的习惯取满足条件的最低版本；预发布版本只有范围里点名了才会用
- **托管程序集**：`lib/{tfm}` 挑最合适的一组解出来（优先不限平台的 `net10.0`，`net10.0-windows` 这种排后面），`runtimes/{rid}/lib/{tfm}` 里平台相关的实现也一起解
- **native 库**：`runtimes/{rid}/native` 全部解出来，按 RID 打分选当前平台（`win-x64` 优先，没有就退回同系统的其他架构），P/Invoke 时按文件名 / 加 `lib` 前缀的名字查
- 只有 `.nuspec` 的元包（例如 `Microsoft.NETCore.Platforms`）算安装成功，不报"没有 lib"
- 卸载组件不影响这些包，多个组件共用同一份

## 打包

组件工程引用 `build/ComponentPackage.targets` 并写一个 `Meta.cs`：

```xml
<ItemGroup>
  <ProjectReference Include="..\build\MetaGen\MetaGen.csproj" ReferenceOutputAssembly="false" />
</ItemGroup>
<Import Project="..\build\ComponentPackage.targets" />
```

```csharp
public static class Meta
{
    public static MetaObj Info = new()
    {
        Name = "ProxyDemo",
        Id = "proxydemo",
        Version = "1.0.0",
        Description = "示例代理",
        Type = ComponentType.Proxy,
        Dlls = ["ProxyDemo.dll"],
        Deps = []
    };
}
```

构建后自动生成 `Meta.json`，并把清单声明的 dll 打成 `{AssemblyName}.zip`，直接拖进面板安装即可。

## 组件 id 和 http 路径

`RegisterHttpPath` 的路径由组件 id 决定（实例再加 uuid），所以：

- 插件 / 代理：`/api/{组件id}`
- 实例：`/api/{组件id}/{实例uuid}`

改组件 id 等于改接口地址，已经在用的回调地址要一起改。

## 组件之间的依赖

- 依赖别的组件只写 `Deps`，**不要把对方的 dll 放进自己的 `Dlls`**
- 加载上下文会先找自己目录里的 dll，打进包就等于每个组件各加载一份，类型标识不再相同
- 组件依赖会按拓扑序加载，被依赖的组件先加载；卸载时会先卸载依赖它的组件
- 依赖检查不通过（缺组件 / 版本不够）时组件会被标记成 `blocked`，不能启用

## 安装

- 面板「组件」页上传 zip，或者填本地 zip / 目录路径
- 安装后按启用记录自动加载并启用；运行库只加载
- 卸载只是运行时行为，重启后会重新加载；要彻底移除用「删除」
