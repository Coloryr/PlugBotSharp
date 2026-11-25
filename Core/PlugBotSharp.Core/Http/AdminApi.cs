using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core.Http;

/// <summary>
/// 管理面板接口：组件列表、安装、加载卸载、实例管理
/// </summary>
internal static class AdminApi
{
    /// <summary>
    /// 注册 /api/admin 下的接口
    /// </summary>
    /// <param name="app">asp 应用</param>
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/admin");

        group.MapGet("/overview", Overview);
        group.MapGet("/certs", Certs);
        group.MapGet("/components", Components);
        // 转成 Delegate，避免被当成 RequestDelegate 丢掉返回值
        group.MapPost("/components/install", (Delegate)Install);
        group.MapPost("/components/{id}/load", Load);
        group.MapPost("/components/{id}/unload", Unload);
        group.MapPost("/components/{id}/enable", Enable);
        group.MapPost("/components/{id}/disable", Disable);
        group.MapDelete("/components/{id}", Delete);
        group.MapPost("/components/{id}/reload", Reload);
        group.MapGet("/components/{id}/config", ComponentConfig);
        group.MapPost("/components/{id}/config", (Delegate)SaveComponentConfig);

        group.MapGet("/instances", Instances);
        group.MapPost("/instances", (Delegate)CreateInstance);
        group.MapPost("/instances/{uuid}/load", LoadInstance);
        group.MapPost("/instances/{uuid}/reload", ReloadInstance);
        group.MapGet("/instances/{uuid}/config", InstanceConfig);
        group.MapPost("/instances/{uuid}/config", (Delegate)SaveInstanceConfig);
        group.MapPost("/instances/{uuid}/enable", EnableInstance);
        group.MapPost("/instances/{uuid}/disable", DisableInstance);
        group.MapDelete("/instances/{uuid}", DeleteInstance);
    }

    /// <summary>
    /// 总览：版本、运行时长、组件与实例数量
    /// </summary>
    private static IResult Overview()
    {
        var components = Collect();
        var instances = InstanceManager.List();

        return Ok(new
        {
            version = typeof(BotManager).Assembly.GetName().Version?.ToString(),
            startTime = BotManager.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
            uptime = (DateTime.Now - BotManager.StartTime).ToString(@"d\.hh\:mm\:ss"),
            components = new
            {
                total = components.Count,
                plugins = components.Count(x => x.Meta.Type == ComponentType.Plugin),
                proxies = components.Count(x => x.Meta.Type == ComponentType.Proxy),
                libs = components.Count(x => x.Meta.Type == ComponentType.Lib),
                loaded = components.Count(x => x.Assembly is not null),
                enabled = components.Count(x => x.Assembly?.State == ComponentState.Enabled),
                blocked = components.Count(x => ComponentManager.IsComponentBlocked(x.Meta.Id))
            },
            instances = new
            {
                total = instances.Count,
                loaded = instances.Count(x => x.Loaded),
                enabled = instances.Count(x => x.Enabled)
            }
        });
    }

    /// <summary>
    /// 证书仓库里的证书，只返回名字和文件名，不返回密码
    /// </summary>
    private static IResult Certs()
    {
        return Ok(CertStore.List()
            .Select(x => new { name = x.Name, file = Path.GetFileName(x.File) })
            .ToList());
    }

    /// <summary>
    /// 组件列表，包含已加载和只在磁盘上的组件
    /// </summary>
    private static IResult Components()
    {
        return Ok(Collect().Select(x => ToView(x.Assembly, x.Meta, x.Folder)).ToList());
    }

    /// <summary>
    /// 安装组件：上传 zip，或者指定本地 zip / 目录
    /// </summary>
    private static async Task<IResult> Install(HttpContext ctx)
    {
        MetaObj? meta = null;
        string error;

        try
        {
            if (ctx.Request.HasFormContentType)
            {
                var form = await ctx.Request.ReadFormAsync(ctx.RequestAborted);
                var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();

                if (file is null || file.Length == 0)
                {
                    return Fail("没有收到安装包");
                }

                await using var stream = file.OpenReadStream();

                if (!ComponentInstaller.TryInstallZip(stream, out meta, out error))
                {
                    return Fail(error);
                }
            }
            else
            {
                var obj = await ReadJson(ctx, SourceGenerationContext.Default.AdminInstallObj);
                var path = obj?.Path;

                if (string.IsNullOrWhiteSpace(path))
                {
                    return Fail("缺少安装路径");
                }

                if (Directory.Exists(path))
                {
                    if (!ComponentInstaller.TryInstallFolder(path, out meta, out error))
                    {
                        return Fail(error);
                    }
                }
                else if (File.Exists(path))
                {
                    using var stream = File.OpenRead(path);

                    if (!ComponentInstaller.TryInstallZip(stream, out meta, out error))
                    {
                        return Fail(error);
                    }
                }
                else
                {
                    return Fail(BotLang.GetLang(BotLangKey.ComponentPathInvalid, path));
                }
            }

            if (meta is null)
            {
                return Fail(BotLang.GetLang(BotLangKey.ComponentInstallNoManifest));
            }

            // 安装完成后按启用记录直接加载并启用，运行库没有启用这一步
            var loaded = ComponentLoader.LoadComponent(meta.Id);
            var enabled = loaded
                && meta.Type != ComponentType.Lib
                && StateManager.IsComponentEnabled(meta.Id)
                && ComponentManager.EnableComponent(meta.Id);

            BotManager.Logger?.Info(BotLang.GetLang(BotLangKey.ComponentInstallSuccess, meta.Id));

            return Ok(new { id = meta.Id, loaded, enabled },
                BotLang.GetLang(BotLangKey.ComponentInstallSuccess, meta.Id));
        }
        catch (Exception e)
        {
            BotManager.Logger?.Error(e, BotLang.GetLang(BotLangKey.ComponentInstallFail, meta?.Id ?? "-", e.Message));
            return Fail(BotLang.GetLang(BotLangKey.ComponentInstallFail, meta?.Id ?? "-", e.Message));
        }
    }

    /// <summary>
    /// 加载组件
    /// </summary>
    private static IResult Load(string id)
    {
        if (ComponentManager.Find(id) is not null)
        {
            return Ok(new { id }, BotLang.GetLang(BotLangKey.ComponentLoaded, id));
        }

        if (!ComponentLoader.LoadComponent(id))
        {
            return Fail(BotLang.GetLang(BotLangKey.ComponentLoadFail, id));
        }

        return Ok(new { id }, BotLang.GetLang(BotLangKey.ComponentLoaded, id));
    }

    /// <summary>
    /// 卸载组件，依赖它的组件会一起卸载
    /// </summary>
    private static IResult Unload(string id)
    {
        if (!ComponentManager.IsLoaded(id))
        {
            return Fail(BotLang.GetLang(BotLangKey.ComponentNotFound, id));
        }

        if (!ComponentManager.UnloadComponent(id))
        {
            return Fail(BotLang.GetLang(BotLangKey.ComponentLoadFail, id));
        }

        return Ok(new { id }, BotLang.GetLang(BotLangKey.ComponentUnloaded, id));
    }

    /// <summary>
    /// 启用组件
    /// </summary>
    private static IResult Enable(string id)
    {
        if (!ComponentManager.EnableComponent(id))
        {
            return Fail(BotLang.GetLang(BotLangKey.ComponentEnableFail, id));
        }

        return Ok(new { id }, BotLang.GetLang(BotLangKey.ComponentStateChanged, id, true));
    }

    /// <summary>
    /// 禁用组件
    /// </summary>
    private static IResult Disable(string id)
    {
        if (!ComponentManager.DisableComponent(id))
        {
            return Fail(BotLang.GetLang(BotLangKey.ComponentNotSupported, id));
        }

        return Ok(new { id }, BotLang.GetLang(BotLangKey.ComponentStateChanged, id, false));
    }

    /// <summary>
    /// 删除组件，先卸载再删除目录
    /// </summary>
    private static IResult Delete(string id)
    {
        if (ComponentManager.IsLoaded(id))
        {
            ComponentManager.UnloadComponent(id);
        }

        if (!ComponentInstaller.TryDelete(id, out var error))
        {
            return Fail(error);
        }

        return Ok(new { id }, BotLang.GetLang(BotLangKey.ComponentDeleteSuccess, id));
    }

    /// <summary>
    /// 重载组件配置：Disable → Reload → Enable
    /// </summary>
    private static IResult Reload(string id)
    {
        if (!ComponentManager.ReloadComponent(id))
        {
            return Fail(BotLang.GetLang(BotLangKey.ComponentReloadFail, id));
        }

        return Ok(new { id }, BotLang.GetLang(BotLangKey.ComponentReloaded, id));
    }

    /// <summary>
    /// 读取组件配置项，面板按 type 生成表单
    /// </summary>
    private static IResult ComponentConfig(string id)
    {
        if (!ComponentManager.IsLoaded(id))
        {
            return Fail(BotLang.GetLang(BotLangKey.ComponentNotFound, id));
        }

        if (!ComponentManager.TryGetConfigMap(id, out var map))
        {
            return Fail(BotLang.GetLang(BotLangKey.ComponentConfigNotSupported, id));
        }

        return Ok(map);
    }

    /// <summary>
    /// 保存组件配置项
    /// </summary>
    private static async Task<IResult> SaveComponentConfig(HttpContext ctx, string id)
    {
        if (!ComponentManager.IsLoaded(id))
        {
            return Fail(BotLang.GetLang(BotLangKey.ComponentNotFound, id));
        }

        var map = await ReadJson(ctx, ConfigMapContext.Default.DictionaryStringConfigMap);

        if (map is null || map.Count == 0)
        {
            return Fail("请求体无效");
        }

        if (!ComponentManager.TryUpdateConfigMap(id, map))
        {
            return Fail(BotLang.GetLang(BotLangKey.ComponentConfigNotSupported, id));
        }

        return Ok(new { id }, BotLang.GetLang(BotLangKey.ComponentConfigSaved, id));
    }

    /// <summary>
    /// 实例列表
    /// </summary>
    private static IResult Instances()
    {
        return Ok(InstanceManager.List().Select(x => new
        {
            uuid = x.Uuid.ToString(),
            proxy = x.Proxy,
            name = x.Name,
            folder = x.Folder,
            loaded = x.Loaded,
            enabled = x.Enabled,
            state = x.Loaded ? x.Enabled ? "enabled" : "disabled" : "unloaded",
            states = InstanceManager.GetStates(x.Uuid)
        }).ToList());
    }

    /// <summary>
    /// 创建实例
    /// </summary>
    private static async Task<IResult> CreateInstance(HttpContext ctx)
    {
        var obj = await ReadJson(ctx, SourceGenerationContext.Default.AdminInstanceObj);

        if (obj is null)
        {
            return Fail("请求体无效");
        }

        if (!InstanceManager.TryCreate(obj.Proxy, obj.Name ?? "", out var uuid, out var loaded, out var error))
        {
            return Fail(error);
        }

        return Ok(new
        {
            uuid = uuid.ToString(),
            loaded,
            // 加载失败时目录已经建好，提示用户补配置
            warning = loaded ? "" : error
        }, BotLang.GetLang(BotLangKey.InstanceCreateSuccess, uuid));
    }

    /// <summary>
    /// 加载实例
    /// </summary>
    private static IResult LoadInstance(string uuid)
    {
        if (!TryParseUuid(uuid, out var id))
        {
            return Fail(BotLang.GetLang(BotLangKey.InstanceNotFound, uuid));
        }

        if (!InstanceManager.TryLoad(id, out var error))
        {
            return Fail(error);
        }

        return Ok(new { uuid }, BotLang.GetLang(BotLangKey.InstanceLoadSuccess, uuid));
    }

    /// <summary>
    /// 启用实例
    /// </summary>
    private static IResult EnableInstance(string uuid)
    {
        if (!TryParseUuid(uuid, out var id))
        {
            return Fail(BotLang.GetLang(BotLangKey.InstanceNotFound, uuid));
        }

        if (!InstanceManager.TryEnable(id, out var error))
        {
            return Fail(error);
        }

        return Ok(new { uuid }, BotLang.GetLang(BotLangKey.InstanceStateChanged, uuid, true));
    }

    /// <summary>
    /// 禁用实例
    /// </summary>
    private static IResult DisableInstance(string uuid)
    {
        if (!TryParseUuid(uuid, out var id))
        {
            return Fail(BotLang.GetLang(BotLangKey.InstanceNotFound, uuid));
        }

        if (!InstanceManager.TryDisable(id, out var error))
        {
            return Fail(error);
        }

        return Ok(new { uuid }, BotLang.GetLang(BotLangKey.InstanceStateChanged, uuid, false));
    }

    /// <summary>
    /// 重载实例配置：Disable 实例 → 组件 Reload 实例 → Reload 实例 → Enable 实例
    /// </summary>
    private static IResult ReloadInstance(string uuid)
    {
        if (!TryParseUuid(uuid, out var id))
        {
            return Fail(BotLang.GetLang(BotLangKey.InstanceNotFound, uuid));
        }

        if (!InstanceManager.TryReload(id, out var error))
        {
            return Fail(error);
        }

        return Ok(new { uuid }, BotLang.GetLang(BotLangKey.InstanceReloaded, uuid));
    }

    /// <summary>
    /// 读取实例配置项
    /// </summary>
    private static IResult InstanceConfig(string uuid)
    {
        if (!TryParseUuid(uuid, out var id))
        {
            return Fail(BotLang.GetLang(BotLangKey.InstanceNotFound, uuid));
        }

        if (!InstanceManager.TryGetConfigMap(id, out var map))
        {
            return Fail(BotLang.GetLang(BotLangKey.InstanceConfigNotSupported, uuid));
        }

        return Ok(map);
    }

    /// <summary>
    /// 保存实例配置项
    /// </summary>
    private static async Task<IResult> SaveInstanceConfig(HttpContext ctx, string uuid)
    {
        if (!TryParseUuid(uuid, out var id))
        {
            return Fail(BotLang.GetLang(BotLangKey.InstanceNotFound, uuid));
        }

        var map = await ReadJson(ctx, ConfigMapContext.Default.DictionaryStringConfigMap);

        if (map is null || map.Count == 0)
        {
            return Fail("请求体无效");
        }

        if (!InstanceManager.TryUpdateConfigMap(id, map))
        {
            return Fail(BotLang.GetLang(BotLangKey.InstanceConfigNotSupported, uuid));
        }

        return Ok(new { uuid }, BotLang.GetLang(BotLangKey.InstanceConfigSaved, uuid));
    }

    /// <summary>
    /// 删除实例，同时删除实例目录
    /// </summary>
    private static IResult DeleteInstance(string uuid)
    {
        if (!TryParseUuid(uuid, out var id))
        {
            return Fail(BotLang.GetLang(BotLangKey.InstanceNotFound, uuid));
        }

        if (!InstanceManager.TryRemove(id, true, out var error))
        {
            return Fail(error);
        }

        return Ok(new { uuid }, BotLang.GetLang(BotLangKey.InstanceRemoveSuccess, uuid));
    }

    /// <summary>
    /// 收集组件：已加载的加上磁盘上未加载的
    /// </summary>
    private static List<(BaseAssembly? Assembly, MetaObj Meta, string Folder)> Collect()
    {
        return ComponentManager.ListAll();
    }

    /// <summary>
    /// 组件转成面板用的结构
    /// </summary>
    /// <param name="assembly">已加载的组件，未加载时为 null</param>
    /// <param name="meta">组件元数据</param>
    /// <param name="folder">组件目录</param>
    private static object ToView(BaseAssembly? assembly, MetaObj meta, string folder)
    {
        var blocked = ComponentManager.IsComponentBlocked(meta.Id);
        var state = ComponentManager.GetState(meta.Id);

        return new
        {
            id = meta.Id,
            name = meta.Name,
            version = meta.Version,
            type = meta.Type.ToString(),
            description = meta.Description,
            dlls = meta.Dlls ?? [],
            deps = (meta.Deps ?? []).Select(x => new { id = x.Id, type = x.Type.ToString(), version = x.Version }).ToList(),
            loaded = assembly is not null,
            enabled = assembly?.State == ComponentState.Enabled,
            blocked,
            state,
            folder,
            states = StateRegistry.Get(ComponentManager.FindObject(meta.Id))
        };
    }

    /// <summary>
    /// 读取 json 请求体
    /// </summary>
    private static Task<T?> ReadJson<T>(HttpContext ctx, JsonTypeInfo<T> info)
    {
        return ApiUtil.Read(ctx, info);
    }

    /// <summary>
    /// 解析实例 uuid
    /// </summary>
    private static bool TryParseUuid(string value, out Guid uuid)
    {
        return Guid.TryParse(value, out uuid);
    }

    /// <summary>
    /// 成功响应
    /// </summary>
    private static IResult Ok(object? data = null, string message = "")
    {
        return ApiUtil.Ok(data, message);
    }

    /// <summary>
    /// 失败响应
    /// </summary>
    private static IResult Fail(string message, int status = StatusCodes.Status400BadRequest)
    {
        return ApiUtil.Fail(message, status);
    }
}