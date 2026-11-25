# 工程结构、构建和运行

## 工程结构

| 目录 | 项目 | 说明 |
| --- | --- | --- |
| `Core/` | `PlugBotSharp.Core` | 宿主核心，引用 `Microsoft.AspNetCore.App`，内嵌管理面板 |
| `Lib/` | `DemoLib` | 示例运行库（id `demolib`，Type `Lib`），演示 Lib 组件的写法 |
| `Lib/` | `PlugBotSharp.QQLib` | QQ 平台模型（消息体 / 键盘 / 卡片）和 `BaseMessage` ↔ QQ 消息转换 |
| `Lib/` | `PlugBotSharp.OneBotLib` | OneBot 11 消息段模型和 `BaseMessage` 转换、平台专属消息段 |
| `Plugin/` | `DemoPlugin` | 示例插件：演示消息事件、各类消息体和面板配置 |
| `Proxy/` | `ProxyDemo` | 示例代理：演示怎么把外部 http 消息转给插件、再把回复发回去 |
| `Proxy/` | `PlugBotSharp.QQProxy` | 官方 QQ 通信代理组件，依赖 `qqlib` |
| `Proxy/` | `PlugBotSharp.OneBotProxy` | OneBot 11 / 12 通信代理组件，依赖 `onebotlib` |
| `Proxy/` | `PlugBotSharp.GsCoreProxy` | gsuid_core 通信代理组件（id `gscoreproxy`） |
| 根目录 | `PlugBotSharp.Run` | 启动入口，运行目录为程序所在目录 |
| 根目录 | `PlugBotSharp.Web` | Vue 3 + Vite 管理面板 |
| 根目录 | `PlugBotSharp.Tests` | xUnit 测试 |
| `build/` | `MetaGen` | 从 `Meta.cs` 生成 `Meta.json` 的工具（可打包成 NuGet） |
| `build/` | `ComponentPackage.targets` | 组件打包：生成清单并把清单里声明的 dll 打成一个 zip |

`PlugBotSharp.Core` 内部按职责分目录，命名空间还是 `PlugBotSharp.Core`，组件里的 `using` 不用改：

| 目录 | 放什么 |
| --- | --- |
| `Components/` | 组件装载和生命周期：`IComponent`、`ComponentLoader`、`ComponentManager`、`ComponentInstaller`、`InstanceManager`、`StateManager`、`BaseAssembly` |
| `Register/` | 注册阶段的注册器和句柄：`IRegister`、`ComponentRegister`、`RaiseHandle`、`StateHandle`、`SendHandle`、`ProxyEventHandle` |
| `Events/` | 事件中心：`EventManager`、`MessageEvent`、`PluginEvent`、`ProxyEvent`、`PluginEventName` |
| `Messages/` | 统一消息模型：`BaseMessage` 和各个消息类型 |
| 其他 | `Auth/`、`Configs/`、`Http/`、`Lib/`、`Nuget/`、`Plugin/`、`Proxy/`，命名空间和目录同名 |

根目录放宿主入口和小工具：`BotManager`（启动 / 加载）、`ConsoleManager`（控制台指令）、`HttpManager`（管理面板）、`ComponentApi`（组件能调的宿主接口）、`CertStore`（证书仓库）、`Ed25519`（签名校验）、`I18`（多语言）。

## 构建

```bash
dotnet build PlugBotSharp.slnx
dotnet test PlugBotSharp.Tests
```

构建完 `PlugBotSharp.Run/bin/Debug/net10.0` 就是一个**完整的运行目录**：宿主 dll 在根上，八个组件已经解压到 `Components/{组件id}`，直接双击 `PlugBotSharp.Run.exe` 就能跑（`Run` 工程的 `DeployComponents` 目标负责解压，组件只参与构建、不进引用）。

这个目录同时就是运行目录，`Nugets`、`Configs`（面板账户、组件配置）、`Instances`、`Config.json`、`Logs` 都是运行时数据。构建只覆盖解压 `Components/{组件id}`，不删这些目录，所以重新构建、重启都不会丢账户，也不会重新下载 NuGet 包。

面板是 Vue 3 + Vite，构建 `PlugBotSharp.Core` 时会自动跑 `npm run build` 并把 `dist` 嵌进 dll，不需要单独起前端服务；改前端时用 `-p:BuildAdminPanel=false` 跳过，避免每次都重新打包。

## 运行目录

```
Config.json                    面板端口和证书
Components/{组件id}/            装好的组件，Meta.json + dll
Instances/{实例uuid}/          实例定义和实例配置
Configs/                       账户、会话、组件启用状态、组件配置
Certs/                         证书
Nugets/                        自动补全的 NuGet 包
Logs/yyyy-MM-dd.log            文件日志
```

## 启停与重载

面板「组件」「实例」页和控制台指令都能做下面这些，不用重启进程：

| 操作 | 效果 |
| --- | --- |
| 加载 | 把组件目录读进 `AssemblyLoadContext`，调 `Init → Load` |
| 启用 / 禁用 | `Enable` / `Disable`，禁用时代理会停掉实例、摘掉 http 接口 |
| 重载 | `Disable → Reload → Enable`，保存设置后面板会自动调 |
| 卸载 | 先卸载依赖它的组件，再回收程序集，手动 `Free` native 库 |
| 删除 | 卸载后删掉组件目录（配置目录保留） |

- 组件的启用状态记在 `Configs/ComponentState.json`，实例的记在 `Configs/InstanceState.json`，重启后按记录恢复
- 依赖不满足的组件会被标记成 `blocked`，不能启用，面板上显示「依赖缺失」
- 卸载 / 禁用都不改磁盘，重启后还是会加载回来

## 面板 HTTP 接口

| 路径 | 说明 |
| --- | --- |
| `GET /api/auth/state` | 是否已初始化、是否已登录（公开） |
| `POST /api/auth/init` | 创建初始账户，只能成功一次（公开） |
| `POST /api/auth/login` / `logout` | 登录 / 退出 |
| `GET /api/status` | 基础状态（公开） |
| `/api/admin/**` | 面板接口，需要登录 |
| `GET/POST /api/admin/components/{id}/config` | 组件设置项，读 `ConfigMap`、写 `UpdateMap` |
| `POST /api/admin/components/{id}/reload` | 重载组件配置 |
| `GET/POST /api/admin/instances/{uuid}/config` | 实例设置项，同上 |
| `POST /api/admin/instances/{uuid}/reload` | 重载实例配置 |
| `/api/{组件id}` | 组件自己注册的接口（公开） |

响应统一为 `{ "ok": bool, "message": string, "data": any }`。

## 日志和控制台

日志同时输出到控制台和 `Logs/yyyy-MM-dd.log`，用 NLog。组件里用 `LogManager.GetLogger("名字")` 拿自己的 logger。

进程运行时控制台可以输入：

| 指令 | 说明 |
| --- | --- |
| `help` / `?` | 帮助 |
| `status` / `st` | 运行状态 |
| `components [type]` | 组件列表，`type` 可选 `plugin` / `proxy` / `lib` |
| `instances` / `inst` | 实例列表 |
| `load <id>` / `unload <id>` | 加载 / 卸载组件 |
| `enable <id>` / `disable <id>` | 启用 / 禁用组件 |
| `clear` / `cls` | 清屏 |
| `stop` / `exit` / `quit` | 停止并退出 |

## 测试约定

- 测试里不允许依赖真实程序集，组件用 `PlugBotSharp.Tests` 里的假实现（`FakePlugin`、`FakeProxy`）
- 测试之间会清空事件注册和组件字典，不要假设顺序
- 网络相关的测试用本地临时服务器或构造报文，不访问外网

## 已知限制

- 卸载组件时手动 `Free` native 库，前提是组件自己的线程已经停止
- 登录防爆破状态是内存态，进程重启后计数清零
- OneBot 反向 WS 模式目前只能收事件，发送仍走 HTTP API
