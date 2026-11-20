# PlugBotSharp

基于 .NET 10 的机器人框架，插件、通信代理、运行库全部是可热插拔的组件，自带 Vue 管理面板和控制台指令。

- 组件独立 `AssemblyLoadContext`，支持卸载后删除文件
- 组件依赖（组件 / NuGet）自动补全和版本校验
- 管理面板（Vue 3 + Vite）编译进 `PlugBotSharp.Core.dll`，单文件分发
- 登录、多账户、会话管理与防爆破
- 控制台指令实时查看和调整运行状态

## 解决方案结构

| 项目 | 说明 |
| --- | --- |
| `PlugBotSharp.Core` | 宿主核心，引用 `Microsoft.AspNetCore.App`，内嵌管理面板 |
| `PlugBotSharp.Core.Lib` | 核心运行库组件（id `core`，Type `Lib`），插件通常依赖它 |
| `PlugBotSharp.QQProxy` | 官方 QQ 通信代理组件 |
| `DemoPlugin` | 示例插件 |
| `PlugBotSharp.Run` | 启动入口，运行目录为程序所在目录 |
| `PlugBotSharp.Web` | Vue 3 + Vite 管理面板 |
| `PlugBotSharp.Tests` | xUnit 测试 |
| `build/MetaGen` | 从 `Meta.cs` 生成 `Meta.json` 的工具（可打包成 NuGet） |
| `build/ComponentPackage.targets` | 组件打包：生成清单并把清单里声明的 dll 打成一个 zip |

## 快速开始

```bash
dotnet build PlugBotSharp.slnx
dotnet run --project PlugBotSharp.Run
```

首次启动会创建运行目录，然后：

1. 浏览器打开 <http://127.0.0.1:10000>
2. 创建初始管理员账户（**只能创建这一个**，之后的账户在面板里加）
3. 在「组件」页安装组件 zip，在「实例」页创建实例

运行目录默认是程序所在目录（`AppContext.BaseDirectory`）。`Config.json` 只有端口一项：

```json
{ "Port": 10000 }
```

面板是必须启动的，`Port` 为 0 或端口被占用会直接退出进程。

## 运行时目录

```
Config.json                    面板端口
Components/
  {组件id}/
    Meta.json                  组件清单
    *.dll
    runtimes/...               可选，native 库
Instances/
  {实例uuid}/
    Instance.json              实例定义
    config.json               实例配置，由组件自己读写
Configs/
  Accounts.json                面板账户（PBKDF2 哈希）
  Sessions.json                登录会话（只存 token 的 SHA-256）
  ComponentState.json          组件启用状态
  InstanceState.json           实例启用状态
  {组件id}/                    组件配置
Nugets/
  {包id}/{版本}/               自动补全的 NuGet 包
Logs/
  yyyy-MM-dd.log              文件日志
```

## 组件

组件目录里只需要一个 `Meta.json`（旧的 `Plugin.json` / `Proxy.json` 已不再识别）：

```json
{
  "Name": "QQProxy",
  "Id": "qq",
  "Version": "1.0.0",
  "Description": "官Q机器人",
  "Type": "Proxy",
  "Dlls": ["PlugBotSharp.QQProxy.dll"],
  "Deps": [
    { "Type": "Component", "Id": "core", "Version": ">=1.0.0" },
    { "Type": "Nuget", "Id": "SkiaSharp", "Version": "4.157.0" }
  ]
}
```

- `Type`：`Plugin` / `Proxy` / `Lib`
- `Dlls`：真正属于这个组件的程序集，会被打在安装包里
- `Deps`：`Component` 是组件依赖，`Nuget` 会自动下载
- 版本表达式支持 `>=1.0.0`、`>1.0.0`、`<=1.0.0`、`<1.0.0`、`=1.0.0` / `==1.0.0`（等于）、逗号组合；裸版本号（`1.0.0`）按最低版本处理，等价于 `>=1.0.0`

### 打包

组件工程引用 `build/ComponentPackage.targets` 并写一个 `Meta.cs`：

```xml
<ItemGroup>
  <ProjectReference Include="..\build\MetaGen\MetaGen.csproj" ReferenceOutputAssembly="false" />
</ItemGroup>
<Import Project="..\build\ComponentPackage.targets" />
```

构建后自动生成 `Meta.json`，并把清单声明的 dll 打成 `{AssemblyName}.zip`，直接拖进面板安装即可。

### 生命周期

| 类型 | 生命周期 |
| --- | --- |
| 运行库 `Lib` | `Init → Load → Unload`（不调用 Enable / Disable） |
| 插件 `Plugin` | `Init → Load → Enable → Disable → Unload` |
| 代理 `Proxy` | `Init → Load → Enable → Disable → Unload` |
| 实例 | `Init → Load → Enable → Tick(1s) → Disable` |

- `Init` 只调用一次，参数是组件配置目录 `Configs/{组件id}`
- 实例的 `Init(path, uuid)` 收到的是实例目录 `Instances/{uuid}`，配置文件放这里
- 禁用组件会自动移除它注册的 http 接口
- 卸载会先卸载依赖它的组件，再卸载整个 `AssemblyLoadContext`，并手动 `Free` 组件加载的 native 库；卸载完成后组件目录可以删除
- 卸载是运行时行为，重启后仍会重新加载；要彻底移除请用「删除」

### 组件里注册 http 接口

```csharp
ComponentApi.RegisterHttpPath(plugin, ctx => { /* /api/{组件id} */ });
ComponentApi.RegisterHttpPath(proxy, ctx => { /* /api/{组件id} */ });
ComponentApi.RegisterHttpPath(instance, ctx => { /* /api/{组件id}/{实例uuid} */ });
```

禁用或卸载时对应接口会自动移除。

## 管理面板

| 页签 | 功能 |
| --- | --- |
| 概览 | 运行时长、组件 / 实例数量、核心版本 |
| 组件 | 组件列表、zip 安装、加载、卸载、启用、禁用、删除、按类型筛选 |
| 实例 | 创建实例、加载、启用、禁用、删除 |
| 账户 | 添加账户、修改 / 重置密码、删除账户、查看和撤销登录会话 |

### 账户与安全

- 密码使用 PBKDF2-SHA256（12 万次迭代 + 随机盐），校验用固定时间比较
- 登录 cookie `pbs.sid`：`HttpOnly` + `SameSite=Strict`，7 天有效，访问时滑动续期，最长 30 天
- 服务端只保存 token 的 SHA-256，`Sessions.json` 泄露也拿不到可用 token
- 防爆破：同一账户 10 分钟内错 5 次锁 10 分钟；同一 IP 错 20 次锁 30 分钟；失败固定延迟 400ms
- 初始账户只能创建一次，之后只能登录后添加
- 至少要保留一个账户，不能删除当前登录的账户
- 面板只监听 `127.0.0.1`，不要直接暴露到公网

## 控制台指令

进程运行时在控制台输入：

| 指令 | 说明 |
| --- | --- |
| `help` / `?` | 帮助 |
| `status` / `st` | 运行状态：版本、时长、面板地址、组件和实例数量 |
| `components [type]` | 组件列表，`type` 可选 `plugin` / `proxy` / `lib` |
| `instances` / `inst` | 实例列表 |
| `load <id>` / `unload <id>` | 加载 / 卸载组件 |
| `enable <id>` / `disable <id>` | 启用 / 禁用组件 |
| `clear` / `cls` | 清屏 |
| `stop` / `exit` / `quit` | 停止并退出 |

日志同时输出到控制台和 `Logs`。以服务方式运行、没有标准输入时，控制台线程会自动退出，不影响进程运行。

## HTTP 接口

| 路径 | 说明 |
| --- | --- |
| `GET /api/auth/state` | 是否已初始化、是否已登录（公开） |
| `POST /api/auth/init` | 创建初始账户，只能成功一次（公开） |
| `POST /api/auth/login` / `logout` | 登录 / 退出 |
| `GET /api/status` | 基础状态（公开） |
| `/api/admin/**` | 面板接口，需要登录 |
| `/api/{组件id}` | 组件自己注册的接口（公开） |

响应统一为 `{ "ok": bool, "message": string, "data": any }`。

## 开发

```bash
dotnet build PlugBotSharp.slnx
dotnet test PlugBotSharp.Tests
```

前端：

```bash
cd PlugBotSharp.Web
npm install
npm run dev     # http://127.0.0.1:5173，/api 代理到 10000
npm run build   # 产物在 dist，编译 Core 时会自动嵌入 dll
```

编译 Core 默认会执行前端构建，`-p:BuildAdminPanel=false` 可以跳过。

## 已知限制

- NuGet 只解析组件直接声明的包，不解析传递依赖；需要 native 库时请在 `Deps` 里显式列出
- 卸载组件时手动 `Free` native 库，前提是组件自己的线程已经停止
- 登录防爆破状态是内存态，进程重启后计数清零
