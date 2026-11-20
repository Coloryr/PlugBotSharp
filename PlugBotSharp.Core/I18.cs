namespace PlugBotSharp.Core;

public enum Lang
{
    zh_cn,
    en_us
}

internal enum BotLangKey
{
    Init,
    ErrorConfigRead,
    ErrorConfigSave,

    PanelDisabled,
    PanelStartFail,
    PanelRequired,

    HttpStart,
    HttpStartError,
    HttpListening,
    HttpStop,
    HttpStopError,
    HttpApiError,
    ApiRegistered,
    ApiRemoved,

    ComponentDirMissing,
    ComponentLoadFail,
    ComponentManifestMissing,
    ComponentManifestInvalid,
    ComponentDuplicate,
    ComponentLoading,
    ComponentTypeMissing,
    ComponentInstanceFail,
    ComponentInitFail,
    ComponentEnableFail,
    ComponentDllMissing,
    ComponentDllsEmpty,
    ComponentDisabled,
    ComponentStateChanged,
    InstanceStateChanged,

    NugetDownloading,
    NugetInstalled,
    NugetDownloadFail,
    NugetNoLib,
    NugetCompleted,
    NugetFailed,
    ComponentDepMissing,
    ComponentDepVersion,

    ComponentInited,
    ComponentLoaded,
    ComponentUnloaded,
    ComponentNotFound,
    ComponentNotSupported,
    ComponentBlocked,

    InstanceManifestMissing,
    InstanceManifestInvalid,
    InstanceProxyMissing,
    InstanceProxyDisabled,
    InstanceLoadFail,
    InstanceEnableFail,
    InstanceStopFail,
    InstanceTickError,
    InstanceDisabled,

    ComponentRegisterFail,
    ComponentUnloadPending,
    ComponentUnloadDependent,
    NativeUnloadFail,

    ComponentInstallSuccess,
    ComponentInstallFail,
    ComponentInstallNoManifest,
    ComponentInstallInUse,
    ComponentDeleteSuccess,
    ComponentDeleteFail,
    ComponentIdInvalid,
    ComponentPathInvalid,
    InstanceCreateSuccess,
    InstanceCreateFail,
    InstanceRemoveSuccess,
    InstanceRemoveFail,
    InstanceNotFound,
    InstanceProxyNotReady,
    ComponentActionFail,
    InstanceActionFail,
    InstanceLoadSuccess,

    AuthRequired,
    AuthLoginFail,
    AuthLoginDone,
    AuthLocked,
    AuthInitDone,
    AuthAlreadyInit,
    AuthUserInvalid,
    AuthPasswordWeak,
    AuthUserExists,
    AuthUserNotFound,
    AuthOldPasswordWrong,
    AuthLastAccount,
    AuthCannotDeleteSelf,
    AuthSessionNotFound,
    AuthAccountAdded,
    AuthPasswordChanged,
    AuthAccountDeleted,
    AuthLogoutDone,
    AuthSessionRemoved,
    AuthSessionsCleared,
    Stopped,

    Stopping,
    ConsoleHint,
    ConsoleHelp,
    ConsoleStatus,
    ConsoleVersion,
    ConsoleUptime,
    ConsoleStartAt,
    ConsolePanel,
    ConsoleAccounts,
    ConsoleComponents,
    ConsoleInstances,
    ConsoleUnknown,
    ConsoleUsage,
    ConsoleFailed,
    ConsoleEmpty,
    ConsoleStateEnabled,
    ConsoleStateDisabled,
    ConsoleStateLoaded,
    ConsoleStateUnloaded,
    ConsoleStateBlocked,
}

internal static class BotLang
{
    /// <summary>
    /// 按当前语言取文案并格式化参数
    /// </summary>
    public static string GetLang(BotLangKey key, params object[] args)
    {
        var text = BotManager.Lang == Lang.en_us ? GetEn(key) : GetZh(key);

        return args.Length == 0 ? text : string.Format(text, args);
    }

    /// <summary>
    /// 中文文案
    /// </summary>
    private static string GetZh(BotLangKey key)
    {
        switch (key)
        {
            case BotLangKey.Init:
                return "核心启动";
            case BotLangKey.ErrorConfigRead:
                return "配置读取失败";
            case BotLangKey.ErrorConfigSave:
                return "配置文件保存错误";

            case BotLangKey.PanelDisabled:
                return "管理面板未启用";
            case BotLangKey.PanelStartFail:
                return "管理面板启动失败";
            case BotLangKey.PanelRequired:
                return "管理面板必须启用，端口不能为 0";

            case BotLangKey.HttpStart:
                return "启动管理面板";
            case BotLangKey.HttpStartError:
                return "管理面板启动异常";
            case BotLangKey.HttpListening:
                return "管理面板监听中 http://127.0.0.1:{0}";
            case BotLangKey.HttpStop:
                return "停止管理面板";
            case BotLangKey.HttpStopError:
                return "管理面板停止异常";
            case BotLangKey.HttpApiError:
                return "插件接口异常: {0}";
            case BotLangKey.ApiRegistered:
                return "插件接口已注册: {0}";
            case BotLangKey.ApiRemoved:
                return "插件接口已移除: {0}";

            case BotLangKey.ComponentDirMissing:
                return "组件目录不存在: {0}";
            case BotLangKey.ComponentLoadFail:
                return "组件加载失败: {0}";
            case BotLangKey.ComponentManifestMissing:
                return "跳过（缺少 {0}）: {1}";
            case BotLangKey.ComponentManifestInvalid:
                return "跳过（{0} 无效）: {1}";
            case BotLangKey.ComponentDuplicate:
                return "跳过（重复的组件 id: {0}）: {1}";
            case BotLangKey.ComponentLoading:
                return "加载组件 {0} ({1}): {2}";
            case BotLangKey.ComponentTypeMissing:
                return "没有找到实现 {0} 的类型";
            case BotLangKey.ComponentInstanceFail:
                return "组件 {0} 创建实例失败: {1}";
            case BotLangKey.ComponentInitFail:
                return "组件 {0} 初始化失败";
            case BotLangKey.ComponentEnableFail:
                return "组件 {0} 启用失败";
            case BotLangKey.ComponentDllMissing:
                return "dll 不存在: {0}";
            case BotLangKey.ComponentDllsEmpty:
                return "{0} 没有声明 Dlls";
            case BotLangKey.ComponentDisabled:
                return "组件 {0} 已禁用，跳过启用";
            case BotLangKey.ComponentStateChanged:
                return "组件 {0} 启用状态: {1}";
            case BotLangKey.InstanceStateChanged:
                return "实例 {0} 启用状态: {1}";

            case BotLangKey.NugetDownloading:
                return "下载 NuGet 包 {0} {1}";
            case BotLangKey.NugetInstalled:
                return "NuGet 包已安装 {0} {1}";
            case BotLangKey.NugetDownloadFail:
                return "NuGet 包下载失败 {0} {1}: {2}";
            case BotLangKey.NugetNoLib:
                return "NuGet 包没有可用的 lib: {0} {1}";
            case BotLangKey.NugetCompleted:
                return "NuGet 依赖补全完成，共 {0} 个包";
            case BotLangKey.NugetFailed:
                return "以下 NuGet 依赖补全失败: {0}";
            case BotLangKey.ComponentDepMissing:
                return "组件 {0} 依赖的组件 {1} 未找到";
            case BotLangKey.ComponentDepVersion:
                return "组件 {0} 依赖 {1} 版本 {2}，当前 {3}";

            case BotLangKey.ComponentInited:
                return "组件 {0} 初始化完成";
            case BotLangKey.ComponentLoaded:
                return "组件 {0} 加载完成";
            case BotLangKey.ComponentUnloaded:
                return "组件 {0} 卸载完成";
            case BotLangKey.ComponentNotFound:
                return "组件 {0} 不存在";
            case BotLangKey.ComponentNotSupported:
                return "组件 {0} 不支持该操作";
            case BotLangKey.ComponentBlocked:
                return "组件 {0} 依赖不满足，已禁用并卸载";

            case BotLangKey.InstanceManifestMissing:
                return "实例 {0} 缺少 Instance.json";
            case BotLangKey.InstanceManifestInvalid:
                return "实例 {0} 的 Instance.json 无效";
            case BotLangKey.InstanceProxyMissing:
                return "实例 {0} 的代理 {1} 不存在";
            case BotLangKey.InstanceProxyDisabled:
                return "实例 {0} 的代理 {1} 未启用";
            case BotLangKey.InstanceLoadFail:
                return "实例 {0} 加载失败";
            case BotLangKey.InstanceEnableFail:
                return "实例 {0} 启用失败";
            case BotLangKey.InstanceStopFail:
                return "实例 {0} 停止异常";
            case BotLangKey.InstanceTickError:
                return "实例 {0} Tick 异常";
            case BotLangKey.InstanceDisabled:
                return "实例 {0} 已禁用，跳过启用";

            case BotLangKey.ComponentRegisterFail:
                return "组件 {0} 还没有加载，不能注册 http 接口";
            case BotLangKey.ComponentUnloadPending:
                return "组件 {0} 的加载上下文还没有回收";
            case BotLangKey.ComponentUnloadDependent:
                return "组件 {0} 依赖 {1}，一并卸载";
            case BotLangKey.NativeUnloadFail:
                return "组件 {0} 的 native 库释放失败";

            case BotLangKey.ComponentInstallSuccess:
                return "组件 {0} 安装完成";
            case BotLangKey.ComponentInstallFail:
                return "组件 {0} 安装失败: {1}";
            case BotLangKey.ComponentInstallNoManifest:
                return "压缩包里没有找到 Meta.json";
            case BotLangKey.ComponentInstallInUse:
                return "组件 {0} 已加载，请先卸载";
            case BotLangKey.ComponentDeleteSuccess:
                return "组件 {0} 已删除";
            case BotLangKey.ComponentDeleteFail:
                return "组件 {0} 删除失败: {1}";
            case BotLangKey.ComponentIdInvalid:
                return "非法的组件 id: {0}";
            case BotLangKey.ComponentPathInvalid:
                return "路径不在组件目录内: {0}";
            case BotLangKey.InstanceCreateSuccess:
                return "实例 {0} 创建完成";
            case BotLangKey.InstanceCreateFail:
                return "实例 {0} 创建失败: {1}";
            case BotLangKey.InstanceRemoveSuccess:
                return "实例 {0} 已删除";
            case BotLangKey.InstanceRemoveFail:
                return "实例 {0} 删除失败: {1}";
            case BotLangKey.InstanceNotFound:
                return "实例 {0} 不存在";
            case BotLangKey.InstanceProxyNotReady:
                return "代理 {0} 未启用，无法创建实例";
            case BotLangKey.ComponentActionFail:
                return "组件 {0} 执行异常";
            case BotLangKey.InstanceActionFail:
                return "实例 {0} 执行异常";
            case BotLangKey.InstanceLoadSuccess:
                return "实例 {0} 已加载";

            case BotLangKey.AuthRequired:
                return "请先登录";
            case BotLangKey.AuthLoginFail:
                return "用户名或密码错误";
            case BotLangKey.AuthLoginDone:
                return "登录成功";
            case BotLangKey.AuthLocked:
                return "尝试次数过多，请 {0} 秒后重试";
            case BotLangKey.AuthInitDone:
                return "初始账户创建完成";
            case BotLangKey.AuthAlreadyInit:
                return "面板已经初始化，不能再创建初始账户";
            case BotLangKey.AuthUserInvalid:
                return "用户名需要 3-32 位，只能用字母、数字、下划线、点和横线";
            case BotLangKey.AuthPasswordWeak:
                return "密码至少 8 位";
            case BotLangKey.AuthUserExists:
                return "账户已存在";
            case BotLangKey.AuthUserNotFound:
                return "账户不存在";
            case BotLangKey.AuthOldPasswordWrong:
                return "原密码错误";
            case BotLangKey.AuthLastAccount:
                return "至少要保留一个账户";
            case BotLangKey.AuthCannotDeleteSelf:
                return "不能删除当前登录的账户";
            case BotLangKey.AuthSessionNotFound:
                return "会话不存在";
            case BotLangKey.AuthAccountAdded:
                return "账户 {0} 已添加";
            case BotLangKey.AuthPasswordChanged:
                return "账户 {0} 的密码已修改";
            case BotLangKey.AuthAccountDeleted:
                return "账户 {0} 已删除";
            case BotLangKey.AuthLogoutDone:
                return "已退出登录";
            case BotLangKey.AuthSessionRemoved:
                return "会话已撤销";
            case BotLangKey.AuthSessionsCleared:
                return "其他会话已全部撤销";
            case BotLangKey.Stopped:
                return "核心已停止";
            case BotLangKey.Stopping:
                return "正在停止核心";
            case BotLangKey.ConsoleHint:
                return "输入 help 查看控制台指令";
            case BotLangKey.ConsoleHelp:
                return "可用指令:\n  help, ?             显示帮助\n  status, st          输出运行状态\n  components [type]   组件列表，type: plugin / proxy / lib\n  instances, inst     实例列表\n  load <id>           加载组件\n  unload <id>         卸载组件\n  enable <id>         启用组件\n  disable <id>        禁用组件\n  clear, cls          清屏\n  stop, exit, quit    停止并退出";
            case BotLangKey.ConsoleStatus:
                return "运行状态";
            case BotLangKey.ConsoleVersion:
                return "版本";
            case BotLangKey.ConsoleUptime:
                return "运行时长";
            case BotLangKey.ConsoleStartAt:
                return "启动时间";
            case BotLangKey.ConsolePanel:
                return "管理面板";
            case BotLangKey.ConsoleAccounts:
                return "账户";
            case BotLangKey.ConsoleComponents:
                return "组件";
            case BotLangKey.ConsoleInstances:
                return "实例";
            case BotLangKey.ConsoleUnknown:
                return "未知指令: {0}，输入 help 查看";
            case BotLangKey.ConsoleUsage:
                return "用法: {0}";
            case BotLangKey.ConsoleFailed:
                return "执行失败: {0}";
            case BotLangKey.ConsoleEmpty:
                return "没有数据";
            case BotLangKey.ConsoleStateEnabled:
                return "运行中";
            case BotLangKey.ConsoleStateDisabled:
                return "已禁用";
            case BotLangKey.ConsoleStateLoaded:
                return "已加载";
            case BotLangKey.ConsoleStateUnloaded:
                return "未加载";
            case BotLangKey.ConsoleStateBlocked:
                return "依赖缺失";
        }

        return "";
    }

    /// <summary>
    /// 英文文案
    /// </summary>
    private static string GetEn(BotLangKey key)
    {
        switch (key)
        {
            case BotLangKey.Init:
                return "Core started";
            case BotLangKey.ErrorConfigRead:
                return "Config read error";
            case BotLangKey.ErrorConfigSave:
                return "Config save error";

            case BotLangKey.PanelDisabled:
                return "Admin panel disabled";
            case BotLangKey.PanelStartFail:
                return "Admin panel start failed";
            case BotLangKey.PanelRequired:
                return "Admin panel is required, port cannot be 0";

            case BotLangKey.HttpStart:
                return "Start admin panel";
            case BotLangKey.HttpStartError:
                return "Admin panel start error";
            case BotLangKey.HttpListening:
                return "Admin panel listening on http://127.0.0.1:{0}";
            case BotLangKey.HttpStop:
                return "Stop admin panel";
            case BotLangKey.HttpStopError:
                return "Admin panel stop error";
            case BotLangKey.HttpApiError:
                return "Plugin api error: {0}";
            case BotLangKey.ApiRegistered:
                return "Plugin api registered: {0}";
            case BotLangKey.ApiRemoved:
                return "Plugin api removed: {0}";

            case BotLangKey.ComponentDirMissing:
                return "Components directory not found: {0}";
            case BotLangKey.ComponentLoadFail:
                return "Component load failed: {0}";
            case BotLangKey.ComponentManifestMissing:
                return "Skipped (missing {0}): {1}";
            case BotLangKey.ComponentManifestInvalid:
                return "Skipped (invalid {0}): {1}";
            case BotLangKey.ComponentDuplicate:
                return "Skipped (duplicate component id: {0}): {1}";
            case BotLangKey.ComponentLoading:
                return "Loading component {0} ({1}): {2}";
            case BotLangKey.ComponentTypeMissing:
                return "No type implementing {0} found";
            case BotLangKey.ComponentInstanceFail:
                return "Component {0} create instance failed: {1}";
            case BotLangKey.ComponentInitFail:
                return "Component {0} init failed";
            case BotLangKey.ComponentEnableFail:
                return "Component {0} enable failed";
            case BotLangKey.ComponentDllMissing:
                return "Dll not found: {0}";
            case BotLangKey.ComponentDllsEmpty:
                return "{0} has no Dlls declared";
            case BotLangKey.ComponentDisabled:
                return "Component {0} is disabled, skip enable";
            case BotLangKey.ComponentStateChanged:
                return "Component {0} enabled: {1}";
            case BotLangKey.InstanceStateChanged:
                return "Instance {0} enabled: {1}";

            case BotLangKey.NugetDownloading:
                return "Downloading NuGet package {0} {1}";
            case BotLangKey.NugetInstalled:
                return "NuGet package installed {0} {1}";
            case BotLangKey.NugetDownloadFail:
                return "NuGet package download failed {0} {1}: {2}";
            case BotLangKey.NugetNoLib:
                return "NuGet package has no usable lib: {0} {1}";
            case BotLangKey.NugetCompleted:
                return "NuGet dependencies ready, {0} package(s)";
            case BotLangKey.NugetFailed:
                return "NuGet dependencies failed: {0}";
            case BotLangKey.ComponentDepMissing:
                return "Component {0} depends on missing component {1}";
            case BotLangKey.ComponentDepVersion:
                return "Component {0} requires {1} {2}, current {3}";

            case BotLangKey.ComponentInited:
                return "Component {0} inited";
            case BotLangKey.ComponentLoaded:
                return "Component {0} loaded";
            case BotLangKey.ComponentUnloaded:
                return "Component {0} unloaded";
            case BotLangKey.ComponentNotFound:
                return "Component {0} not found";
            case BotLangKey.ComponentNotSupported:
                return "Component {0} does not support this operation";
            case BotLangKey.ComponentBlocked:
                return "Component {0} has unsatisfied dependencies, disabled and unloaded";

            case BotLangKey.InstanceManifestMissing:
                return "Instance {0} has no Instance.json";
            case BotLangKey.InstanceManifestInvalid:
                return "Instance {0} has an invalid Instance.json";
            case BotLangKey.InstanceProxyMissing:
                return "Instance {0} proxy {1} not found";
            case BotLangKey.InstanceProxyDisabled:
                return "Instance {0} proxy {1} is not enabled";
            case BotLangKey.InstanceLoadFail:
                return "Instance {0} load failed";
            case BotLangKey.InstanceEnableFail:
                return "Instance {0} enable failed";
            case BotLangKey.InstanceStopFail:
                return "Instance {0} stop error";
            case BotLangKey.InstanceTickError:
                return "Instance {0} tick error";
            case BotLangKey.InstanceDisabled:
                return "Instance {0} is disabled, skip enable";

            case BotLangKey.ComponentRegisterFail:
                return "Component {0} is not loaded, cannot register http api";
            case BotLangKey.ComponentUnloadPending:
                return "Component {0} load context is not unloaded yet";
            case BotLangKey.ComponentUnloadDependent:
                return "Component {0} depends on {1}, unloading it too";
            case BotLangKey.NativeUnloadFail:
                return "Component {0} native library free failed";

            case BotLangKey.ComponentInstallSuccess:
                return "Component {0} installed";
            case BotLangKey.ComponentInstallFail:
                return "Component {0} install failed: {1}";
            case BotLangKey.ComponentInstallNoManifest:
                return "No Meta.json found in the package";
            case BotLangKey.ComponentInstallInUse:
                return "Component {0} is loaded, unload it first";
            case BotLangKey.ComponentDeleteSuccess:
                return "Component {0} deleted";
            case BotLangKey.ComponentDeleteFail:
                return "Component {0} delete failed: {1}";
            case BotLangKey.ComponentIdInvalid:
                return "Invalid component id: {0}";
            case BotLangKey.ComponentPathInvalid:
                return "Path is outside the components directory: {0}";
            case BotLangKey.InstanceCreateSuccess:
                return "Instance {0} created";
            case BotLangKey.InstanceCreateFail:
                return "Instance {0} create failed: {1}";
            case BotLangKey.InstanceRemoveSuccess:
                return "Instance {0} deleted";
            case BotLangKey.InstanceRemoveFail:
                return "Instance {0} delete failed: {1}";
            case BotLangKey.InstanceNotFound:
                return "Instance {0} not found";
            case BotLangKey.InstanceProxyNotReady:
                return "Proxy {0} is not enabled, cannot create instance";
            case BotLangKey.ComponentActionFail:
                return "Component {0} threw an exception";
            case BotLangKey.InstanceActionFail:
                return "Instance {0} threw an exception";
            case BotLangKey.InstanceLoadSuccess:
                return "Instance {0} loaded";

            case BotLangKey.AuthRequired:
                return "Please sign in";
            case BotLangKey.AuthLoginFail:
                return "Invalid user name or password";
            case BotLangKey.AuthLoginDone:
                return "Signed in";
            case BotLangKey.AuthLocked:
                return "Too many attempts, retry in {0} second(s)";
            case BotLangKey.AuthInitDone:
                return "Initial account created";
            case BotLangKey.AuthAlreadyInit:
                return "Panel already initialized";
            case BotLangKey.AuthUserInvalid:
                return "User name must be 3-32 chars: letters, digits, underscore, dot or dash";
            case BotLangKey.AuthPasswordWeak:
                return "Password must be at least 8 characters";
            case BotLangKey.AuthUserExists:
                return "Account already exists";
            case BotLangKey.AuthUserNotFound:
                return "Account not found";
            case BotLangKey.AuthOldPasswordWrong:
                return "Old password is incorrect";
            case BotLangKey.AuthLastAccount:
                return "At least one account must remain";
            case BotLangKey.AuthCannotDeleteSelf:
                return "Cannot delete the account you are signed in with";
            case BotLangKey.AuthSessionNotFound:
                return "Session not found";
            case BotLangKey.AuthAccountAdded:
                return "Account {0} added";
            case BotLangKey.AuthPasswordChanged:
                return "Password of {0} changed";
            case BotLangKey.AuthAccountDeleted:
                return "Account {0} deleted";
            case BotLangKey.AuthLogoutDone:
                return "Signed out";
            case BotLangKey.AuthSessionRemoved:
                return "Session revoked";
            case BotLangKey.AuthSessionsCleared:
                return "All other sessions revoked";
            case BotLangKey.Stopped:
                return "Core stopped";
            case BotLangKey.Stopping:
                return "Stopping core";
            case BotLangKey.ConsoleHint:
                return "Type help for console commands";
            case BotLangKey.ConsoleHelp:
                return "Commands:\n  help, ?             show this help\n  status, st          print runtime status\n  components [type]   list components, type: plugin / proxy / lib\n  instances, inst     list instances\n  load <id>           load component\n  unload <id>         unload component\n  enable <id>         enable component\n  disable <id>        disable component\n  clear, cls          clear screen\n  stop, exit, quit    stop and exit";
            case BotLangKey.ConsoleStatus:
                return "Status";
            case BotLangKey.ConsoleVersion:
                return "Version";
            case BotLangKey.ConsoleUptime:
                return "Uptime";
            case BotLangKey.ConsoleStartAt:
                return "Started";
            case BotLangKey.ConsolePanel:
                return "Panel";
            case BotLangKey.ConsoleAccounts:
                return "Accounts";
            case BotLangKey.ConsoleComponents:
                return "Components";
            case BotLangKey.ConsoleInstances:
                return "Instances";
            case BotLangKey.ConsoleUnknown:
                return "Unknown command: {0}, type help";
            case BotLangKey.ConsoleUsage:
                return "Usage: {0}";
            case BotLangKey.ConsoleFailed:
                return "Failed: {0}";
            case BotLangKey.ConsoleEmpty:
                return "No data";
            case BotLangKey.ConsoleStateEnabled:
                return "enabled";
            case BotLangKey.ConsoleStateDisabled:
                return "disabled";
            case BotLangKey.ConsoleStateLoaded:
                return "loaded";
            case BotLangKey.ConsoleStateUnloaded:
                return "unloaded";
            case BotLangKey.ConsoleStateBlocked:
                return "blocked";
        }

        return "";
    }
}
