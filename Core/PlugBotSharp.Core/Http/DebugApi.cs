using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PlugBotSharp.Api;
using PlugBotSharp.Api.Messages;
using PlugBotSharp.Core.Components;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core.Http;

/// <summary>
/// 面板的调试接口：模拟代理收到一条消息，注入给系统，再把回复收回来
/// </summary>
internal static class DebugApi
{
    /// <summary>
    /// 注册 /api/admin/debug 下的接口
    /// </summary>
    /// <param name="app">asp 应用</param>
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/admin/debug");

        group.MapPost("/message", (Delegate)InjectMessage);
        group.MapGet("/replies", Replies);
        group.MapPost("/reset", Reset);
        group.MapGet("/preview", Preview);
    }

    /// <summary>
    /// 注入一条消息：默认走 Dispatch 发给所有人，选了目标就只发给它
    /// </summary>
    /// <param name="ctx">请求上下文</param>
    private static async Task<IResult> InjectMessage(HttpContext ctx)
    {
        var obj = await ApiUtil.Read(ctx, SourceGenerationContext.Default.DebugMessageObj);

        if (obj is null)
        {
            return ApiUtil.Fail("请求体无效");
        }

        if (string.IsNullOrWhiteSpace(obj.Text))
        {
            return ApiUtil.Fail("缺少 text");
        }

        // 目标：空 = 全部，plugin:{id} = 单个插件，instance:{uuid} = 单个实例
        object? target = null;
        var proxyId = "debug";
        var mode = "all";

        if (!string.IsNullOrWhiteSpace(obj.Scope))
        {
            var index = obj.Scope.IndexOf(':');

            if (index <= 0)
            {
                return ApiUtil.Fail("目标格式无效，应为 plugin:{id} 或 instance:{uuid}");
            }

            var kind = obj.Scope[..index];
            var value = obj.Scope[(index + 1)..];

            if (kind == "plugin")
            {
                target = ComponentManager.FindObject(value);

                if (target is null)
                {
                    return ApiUtil.Fail("插件不存在: " + value);
                }

                mode = "plugin";
            }
            else if (kind == "instance")
            {
                if (!Guid.TryParse(value, out var uuid) || !InstanceManager.TryGetObject(uuid, out target))
                {
                    return ApiUtil.Fail("实例不存在或未加载: " + value);
                }

                mode = "instance";
            }
            else
            {
                return ApiUtil.Fail("目标类型无效: " + kind);
            }
        }

        // 记下当前序号，异步回复从这之后算新的
        var since = DebugSink.Current;

        var message = new ProxyMessage
        {
            Id = proxyId,
            Uuid = DebugSink.Uuid,
            SenderType = ParseSenderType(obj.SenderType),
            Sender = string.IsNullOrWhiteSpace(obj.Sender) ? "debug" : obj.Sender,
            Target = obj.Target ?? "",
            ReplyId = string.IsNullOrWhiteSpace(obj.ReplyId) ? "debug-" + Guid.NewGuid().ToString("N")[..8] : obj.ReplyId,
            Message = new TextMessage(obj.Text)
        };

        var collected = new List<(string Source, BaseMessage Message)>();

        EventManager.HandleMessage(message, target, (owner, reply) => collected.Add((OwnerLabel(owner), reply)));

        return ApiUtil.Ok(new
        {
            injected = new { mode, proxy = proxyId, scope = obj.Scope ?? "", message.Sender, message.Target, message.ReplyId },
            since,
            replies = collected
                .SelectMany(x => DebugSink.Flatten(x.Message).Select(y => new { x.Source, Message = y }))
                .Select(x => new
                {
                    type = x.Message.Type.ToString(),
                    text = DebugSink.Describe(x.Message),
                    source = x.Source
                })
                .ToList()
        });
    }
    /// <summary>
    /// 取注入之后异步回来的回复（长任务插件、gsuid_core 回图之类）
    /// </summary>
    /// <param name="ctx">请求上下文</param>
    private static IResult Replies(HttpContext ctx)
    {
        var since = 0L;

        if (long.TryParse(ctx.Request.Query["since"], out var value))
        {
            since = value;
        }

        var items = DebugSink.Since(since);

        return ApiUtil.Ok(new
        {
            replies = items.Select(x => new
            {
                x.Seq,
                x.Type,
                x.Text,
                x.PreviewMime,
                x.Source,
                time = x.Time.ToString("HH:mm:ss")
            }).ToList()
        });
    }

    /// <summary>
    /// 清空调试终端截下来的回复，面板上点「清空对话」时调
    /// </summary>
    private static IResult Reset()
    {
        DebugSink.Clear();

        return ApiUtil.Ok();
    }

    /// <summary>
    /// 取一条回复里的图片，面板直接用 img 显示
    /// </summary>
    /// <param name="ctx">请求上下文</param>
    private static IResult Preview(HttpContext ctx)
    {
        if (!long.TryParse(ctx.Request.Query["seq"], out var seq))
        {
            return Results.NotFound();
        }

        var data = DebugSink.Image(seq);

        if (data is null)
        {
            return Results.NotFound();
        }

        ctx.Response.Headers.CacheControl = "no-store";

        return Results.File(data, "image/png");
    }

    /// <summary>
    /// 回复的组件对象转成好认的名字，面板上显示谁回的
    /// </summary>
    /// <param name="owner">组件对象</param>
    private static string OwnerLabel(object owner)
    {
        return owner switch
        {
            IBasePlugin plugin => ComponentManager.FindPluginId(plugin) ?? "plugin",
            IBaseProxy proxy => ComponentManager.FindProxyId(proxy) ?? "proxy",
            IProxyInstasnce instance when InstanceManager.TryGetInstance(instance, out var proxyId, out var uuid)
                => proxyId + "/" + uuid[..8],
            _ => owner.GetType().Name
        };
    }

    /// <summary>
    /// 会话类型字符串转枚举
    /// </summary>
    /// <param name="value">group / user</param>
    private static MessageSenderType ParseSenderType(string? value)
    {
        return string.Equals(value, "group", StringComparison.OrdinalIgnoreCase)
            ? MessageSenderType.Group
            : MessageSenderType.User;
    }
}
