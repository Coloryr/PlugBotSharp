using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NLog;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Messages;

namespace ProxyDemo;

/// <summary>
/// 示例代理实例：一个实例对应一个外部程序，http 路径是 /api/proxydemo/{uuid}。
/// 外部程序 POST 一条消息进来 → Dispatch 给插件 → 把插件返回的消息 POST 回回调地址
/// </summary>
public class DemoInstance : IProxyInstasnce
{
    private static readonly Logger Logger = LogManager.GetLogger("ProxyDemo");

    private readonly string _proxyId;
    private readonly HttpClient _http = new();
    private readonly ConcurrentDictionary<string, int> _senders = new();

    private string _configFile = "";
    private InstanceDataObj _data = new();
    private Guid _uuid;
    private bool _enabled;

    private ProxyEventHandle? _dispatch;
    private StateValueHandle? _recvState;
    private StateValueHandle? _sentState;
    private StateKvHandle? _senderState;

    private int _recv;
    private int _sent;

    /// <summary>
    /// 创建实例
    /// </summary>
    /// <param name="proxyId">所属代理组件 id</param>
    public DemoInstance(string proxyId)
    {
        _proxyId = proxyId;
    }

    /// <inheritdoc/>
    public void Init(string path, Guid uuid)
    {
        _configFile = Path.Combine(path, "config.json");
        _uuid = uuid;
    }

    /// <inheritdoc/>
    public bool Load()
    {
        return ReadConfig();
    }

    /// <inheritdoc/>
    public void Reload()
    {
        ReadConfig();
    }

    /// <inheritdoc/>
    public void RegisterEventHook(IRegister register)
    {
        // 代理 / 实例才能拿到的注入句柄：把收到的消息交给插件
        _dispatch = register.RegisterProxyEvent();

        // 外部程序把消息 POST 到这个路径
        register.RegisterHttpPath(HandleAsync);

        // 状态：单值和键值各来一个，面板上能直接看
        _recvState = register.RegisterState("recv", "收到条数", "外部程序推进来的消息条数");
        _sentState = register.RegisterState("sent", "发出条数", "发回外部程序的回复条数");
        _senderState = register.RegisterKvState("senders", "发送者统计", "每个发送者推了几条，键是发送者");
    }

    /// <inheritdoc/>
    public bool Enable()
    {
        _enabled = true;

        return true;
    }

    /// <inheritdoc/>
    public bool Disable()
    {
        _enabled = false;

        return true;
    }

    /// <inheritdoc/>
    public bool Unload()
    {
        _enabled = false;
        _http.Dispose();

        return true;
    }

    /// <inheritdoc/>
    public void Tick()
    {
        // 这个代理是纯被动的，没有需要定时重试的东西
    }

    /// <inheritdoc/>
    public async Task SendAsync(ProxyMessage message, BaseMessage reply)
    {
        // 插件主动发的消息：按实例配置的回调地址发出去
        var target = string.IsNullOrEmpty(message.Target) ? message.Sender : message.Target;

        await SendAsync(target, Describe(reply));
    }

    /// <inheritdoc/>
    public Dictionary<string, ConfigMap> ConfigMap()
    {
        return new()
        {
            ["Callback"] = new ConfigStringMap
            {
                Key = "Callback",
                Value = _data.Callback,
                Default = "",
                MaxLength = 200,
                Description = "插件返回的消息 POST 到这里，留空则把回复放回 http 响应",
                Placeholder = "http://127.0.0.1:8080/reply"
            },
            ["TimeoutSeconds"] = new ConfigIntMap
            {
                Key = "TimeoutSeconds",
                Value = _data.TimeoutSeconds,
                Min = 1,
                Max = 60,
                Default = 10,
                Description = "回调的超时时间（秒）"
            }
        };
    }

    /// <inheritdoc/>
    public void UpdateMap(Dictionary<string, ConfigMap> data)
    {
        if (data.TryGetValue("Callback", out var callback) && callback is ConfigStringMap callbackMap)
        {
            _data.Callback = callbackMap.Value ?? "";
        }

        if (data.TryGetValue("TimeoutSeconds", out var timeout) && timeout is ConfigIntMap timeoutMap)
        {
            _data.TimeoutSeconds = Math.Clamp(timeoutMap.Value, 1, 60);
        }

        Apply();
        SaveConfig();
    }

    /// <summary>
    /// 处理外部程序推进来的消息
    /// </summary>
    /// <param name="ctx">http 上下文</param>
    private async Task HandleAsync(HttpContext ctx)
    {
        if (!_enabled)
        {
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

            return;
        }

        DemoMessageObj? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync(ctx.Request.Body, DemoJsonContext.Default.DemoMessageObj);
        }
        catch
        {
            request = null;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Text))
        {
            await WriteAsync(ctx, new DemoResultObj { Message = "请求体里没有 text" }, StatusCodes.Status400BadRequest);

            return;
        }

        var sender = string.IsNullOrWhiteSpace(request.Sender) ? "unknown" : request.Sender;

        _recv++;
        _recvState?.Set(_recv);

        var count = _senders.AddOrUpdate(sender, 1, (_, old) => old + 1);

        _senderState?.Set(sender, count);

        if (ProxyMain.LogMessage)
        {
            Logger.Info($"收到 {sender}: {request.Text}");
        }

        var message = new ProxyMessage
        {
            Id = _proxyId,
            Uuid = _uuid,
            SenderType = ParseSenderType(request.SenderType),
            Sender = sender,
            Target = request.Target ?? "",
            ReplyId = string.IsNullOrWhiteSpace(request.ReplyId) ? null : request.ReplyId,
            Message = new TextMessage(request.Text)
        };

        // 交给宿主：所有订阅消息事件的组件都会收到，返回值的并集就是这次要回的消息
        var replies = _dispatch?.Dispatch(message) ?? [];

        var callback = string.IsNullOrWhiteSpace(request.Callback) ? _data.Callback : request.Callback;
        var result = new DemoResultObj { Ok = true };

        foreach (var reply in replies)
        {
            _sent++;

            if (string.IsNullOrWhiteSpace(callback))
            {
                result.Replies.Add(new DemoReplyObj
                {
                    Sender = sender,
                    Target = message.Target,
                    Text = Describe(reply)
                });

                continue;
            }

            await SendAsync(message.Target, Describe(reply), callback);
        }

        _sentState?.Set(_sent);

        await WriteAsync(ctx, result, StatusCodes.Status200OK);
    }

    /// <summary>
    /// 把回复发回外部程序
    /// </summary>
    /// <param name="target">回复目标</param>
    /// <param name="text">回复内容</param>
    /// <param name="callback">回调地址，为空用实例配置</param>
    private async Task SendAsync(string target, string text, string? callback = null)
    {
        var url = string.IsNullOrWhiteSpace(callback) ? _data.Callback : callback;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            Logger.Warn($"回调地址无效，回复丢弃: {url}");

            return;
        }

        var reply = new DemoReplyObj
        {
            Sender = target,
            Target = target,
            Text = text
        };

        try
        {
            using var content = JsonContent.Create(reply, DemoJsonContext.Default.DemoReplyObj);
            using var response = await _http.PostAsync(uri, content);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"回调 {url} 失败: {(int)response.StatusCode}");
            }

            if (ProxyMain.LogMessage)
            {
                Logger.Info($"发出 {target}: {text}");
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"回调 {url} 失败");
        }
    }

    /// <summary>
    /// 写 json 响应
    /// </summary>
    /// <param name="ctx">http 上下文</param>
    /// <param name="result">响应内容</param>
    /// <param name="status">状态码</param>
    private static async Task WriteAsync(HttpContext ctx, DemoResultObj result, int status)
    {
        ctx.Response.StatusCode = status;

        await ctx.Response.WriteAsJsonAsync(result, DemoJsonContext.Default.DemoResultObj);
    }

    /// <summary>
    /// 消息转成一段文本，示例里只认几种常见的
    /// </summary>
    /// <param name="message">消息</param>
    private static string Describe(BaseMessage message)
    {
        return message switch
        {
            TextMessage text => text.Text,
            MarkdownMessage markdown => markdown.Content,
            ImageUrlMessage image => image.Url,
            ImageFileMessage image => image.File,
            ReplyMessage reply => "引用 " + reply.MessageId,
            ListMessage list => string.Join(" ", list.Messages.Select(Describe)),
            _ => $"[{message.Type}]"
        };
    }

    /// <summary>
    /// 会话类型字符串转枚举
    /// </summary>
    /// <param name="value">group / user</param>
    private static MessageSenderType ParseSenderType(string value)
    {
        return string.Equals(value, "group", StringComparison.OrdinalIgnoreCase)
            ? MessageSenderType.Group
            : MessageSenderType.User;
    }

    /// <summary>
    /// 读实例配置
    /// </summary>
    private bool ReadConfig()
    {
        try
        {
            if (File.Exists(_configFile))
            {
                var obj = JsonUtils.ToObj(File.ReadAllText(_configFile), DemoJsonContext.Default.InstanceDataObj);

                if (obj is not null)
                {
                    _data = obj;
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "实例配置读取失败");

            return false;
        }

        Apply();

        return true;
    }

    /// <summary>
    /// 写实例配置
    /// </summary>
    private void SaveConfig()
    {
        try
        {
            File.WriteAllText(_configFile, JsonUtils.ToString(_data, DemoJsonContext.Default.InstanceDataObj));
        }
        catch (Exception e)
        {
            Logger.Error(e, "实例配置保存失败");
        }
    }

    /// <summary>
    /// 配置生效
    /// </summary>
    private void Apply()
    {
        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_data.TimeoutSeconds, 1, 60));
    }
}
