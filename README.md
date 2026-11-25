# PlugBotSharp

基于 .NET 10 的机器人框架，插件、通信代理、运行库都是可热插拔的组件，自带 Vue 管理面板和控制台指令。

- 面板和机器人跑在同一个进程里，分发时只需要一个目录
- 组件是一个 zip，拖进面板就能装，加载 / 卸载 / 禁用都不用重启
- 官方带 QQ 机器人（QQProxy）、OneBot 11 / 12（OneBotProxy）和 gsuid_core（GsCoreProxy）三种代理
- 仓库里有三个可以直接抄的示例组件：插件 `DemoPlugin`、代理 `ProxyDemo`、运行库 `DemoLib`
- 登录、多账户、会话管理和登录防爆破都有

想二次开发、写组件、看协议细节，见 [docs/README.md](docs/README.md)。

## 快速开始

```bash
dotnet build PlugBotSharp.slnx
dotnet run --project PlugBotSharp.Run
```

首次启动会创建运行目录，然后：

1. 浏览器打开 <http://127.0.0.1:10000>
2. 创建初始管理员账户（**只能创建这一个**，之后的账户在面板里加），账户存在运行目录的 `Configs/Accounts.json`，重启不会丢
3. 在「组件」页安装组件 zip，在「实例」页创建实例

运行目录默认是程序所在目录（`AppContext.BaseDirectory`）。`Config.json` 默认只有端口：

```json
{ "Port": 10000 }
```

想用 https 就把 IIS 导出的 pfx 放进 `Certs`，在 `Certs.json` 里登记一个名字，然后在配置里填名字：

```json
{
  "Port": 10000,
  "Certificate": "panel"
}
```

`Certs/Certs.json`：

```json
{
  "Items": [
    { "Name": "panel", "File": "panel.pfx", "Password": "******" }
  ]
}
```

面板、QQ 代理的自建 http 服务器、OneBot 代理的反向服务端都用这一份证书，直接填 pfx 路径也兼容。

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
    config.json                实例配置，由组件自己读写
Configs/
  Accounts.json                面板账户（PBKDF2 哈希）
  Sessions.json                登录会话（只存 token 的 SHA-256）
  ComponentState.json          组件启用状态
  InstanceState.json           实例启用状态
  {组件id}/                    组件配置
Certs/
  Certs.json                   证书清单（名字 / 文件 / 密码）
  *.pfx                        IIS 导出的证书，面板和组件共用
Nugets/
  {包id}/{版本}/               自动补全的 NuGet 包
Logs/
  yyyy-MM-dd.log               文件日志
```

## 安装组件

组件包就是一个 zip，里面只有一份 `Meta.json` 和它声明的 dll：

```
Meta.json
PlugBotSharp.QQProxy.dll
```

面板「组件」页可以直接上传 zip，也可以填本地 zip 路径。装好之后：

- 「组件」页加载 / 启用，代理组件启用后才能建实例
- 「实例」页创建实例，点「设置」填 Url、Token、AppId 这类参数
- 保存设置会自动重载，也可以单独点「重载」或「禁用 / 启用」

组件的清单字段、依赖声明和打包方式见 [组件和打包](docs/reference/package.md)。

## 管理面板

| 页签 | 功能 |
| --- | --- |
| 概览 | 运行时长、组件 / 实例数量、核心版本，组件 / 实例的实时状态面板，以及按对象分组的自定义状态监控卡片 |
| 组件 | 组件列表、zip 安装、加载、卸载、启用、禁用、重载、删除、按类型筛选、设置 |
| 实例 | 创建实例、加载、启用、禁用、重载、删除、设置 |
| 调试 | 默认只测插件（代理不参与），可切换走代理链路；虚拟注入消息，同步 / 异步回复都列出来，图片可直接预览，不发到平台 |
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

## 已知限制

- 卸载组件时手动 `Free` native 库，前提是组件自己的线程已经停止
- 登录防爆破状态是内存态，进程重启后计数清零

## 文档

**教程**（按章读，从写第一个插件到发布）：

- [第一章 快速上手](docs/README.md)：选插件还是代理、最小插件 / 代理、生命周期
- [第二章 完整插件制作](docs/chapters/ch02-plugin.md)：建工程、消息解析、消息发送、配置、状态、打包安装
- [第三章 写一个代理](docs/chapters/ch03-proxy.md)
- [第四章 写一个运行库](docs/chapters/ch04-lib.md)
- [第五章 事件系统](docs/chapters/ch05-events.md)：`IRegister`、消息 / 插件 / 代理事件、自定义事件
- [第六章 配置文件](docs/chapters/ch06-config.md)
- [第七章 自定义数据](docs/chapters/ch07-state.md)
- [第八章 远程调试](docs/chapters/ch08-debug.md)
- [第九章 依赖库和运行库](docs/chapters/ch09-deps.md)
- [第十章 打包与发布](docs/chapters/ch10-package.md)
- [第十一章 ComponentApi 进阶](docs/chapters/ch11-componentapi.md)：查 / 管别的组件和实例
- [第十二章 RegisterHttpPath 与代理配合](docs/chapters/ch12-http.md)：对外接口、自建服务器（StartServer）、鉴权、接外部系统再走代理

**参考手册**（按主题查）：

- [消息与插件](docs/reference/plugin.md)：消息类型清单、平台消息、DemoPlugin
- [代理实现](docs/reference/proxy.md)：消息模型、分发链路、QQ / OneBot / GsCore
- [清单与打包](docs/reference/package.md)：`Meta.json`、依赖、NuGet、打包安装
- [接口与 API](docs/reference/api.md)：接口表、`IRegister`、`ComponentApi`、http、证书
- [运行与构建](docs/reference/runtime.md)：工程结构、运行目录、启停重载、面板接口、已知限制
