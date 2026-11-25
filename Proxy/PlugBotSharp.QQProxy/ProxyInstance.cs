using System.Collections.Concurrent;
using NLog;
using Microsoft.AspNetCore.Http;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Configs;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.QQLib;

namespace PlugBotSharp.QQProxy;

public class ProxyInstance : IProxyInstasnce
{
    /// <summary>
    /// 日志
    /// </summary>
    internal static Logger Logger;

    /// <summary>
    /// 配置文件
    /// </summary>
    private string _configFile;
    /// <summary>
    /// 实例配置
    /// </summary>
    private InstanceDataObj _data = new();

    /// <summary>
    /// 实例ID
    /// </summary>
    private Guid _uuid;

    /// <summary>
    /// 所属代理组件 id
    /// </summary>
    private readonly string _proxyId;

    /// <summary>
    /// 凭证
    /// </summary>
    private string? _token;
    /// <summary>
    /// 有效期
    /// </summary>
    private double _expires;
    /// <summary>
    /// 上次更新时间
    /// </summary>
    private DateTime _lastDateTime;

    private bool _update;

    /// <summary>
    /// 拿不到凭证时下次重试的时间，避免每秒刷一次日志
    /// </summary>
    private DateTime _tokenRetryAt;

    private IProxy? _proxy;

    /// <summary>
    /// 分发句柄，注册阶段拿到
    /// </summary>
    private ProxyEventHandle? _dispatch;

    /// <summary>
    /// 发送句柄，回复走它，先过插件的拦截器再真正发出去
    /// </summary>
    private SendHandle? _send;

    /// <summary>
    /// 被动回复的序号，按消息 id 各自从 1 开始递增，平台要求同一条消息的多条回复序号递增
    /// </summary>
    private readonly ConcurrentDictionary<string, ReplySeq> _replySeqs = new();

    /// <summary>
    /// 回复序号超过这个数量就清理一次
    /// </summary>
    private const int ReplySeqLimit = 256;

    /// <summary>
    /// 回复序号保留多久（分钟），被动回复窗口是 60 分钟
    /// </summary>
    private const int ReplySeqLifetime = 60;

    /// <summary>
    /// 回复序号的记录，用来定期清理过期的被动回复窗口
    /// </summary>
    /// <param name="Seq">当前序号</param>
    /// <param name="Created">第一次回复的时间</param>
    private sealed record ReplySeq(int Seq, DateTime Created);

    /// <summary>
    /// 被动回复最多 5 条，到 5 之后不再带 msg_id
    /// </summary>
    private const int PassiveLimit = 5;

    /// <summary>
    /// 每条消息的回复状态：已经转主动 / 停止发送
    /// </summary>
    private sealed class ReplyState
    {
        public bool Active;
        public bool Blocked;
        public DateTime Last = DateTime.Now;
    }

    /// <summary>
    /// 消息 id 到回复状态的映射
    /// </summary>
    private readonly ConcurrentDictionary<string, ReplyState> _replyStates = new();

    /// <summary>
    /// 每个群每天主动消息的配额
    /// </summary>
    private ActiveQuota? _quota;

    /// <summary>
    /// 主动消息配额的状态句柄，面板上显示成 群 -> 已发条数
    /// </summary>
    private StateKvHandle? _quotaState;

    /// <summary>
    /// 创建实例
    /// </summary>
    /// <param name="proxyId">所属代理组件 id</param>
    public ProxyInstance(string proxyId)
    {
        _proxyId = proxyId;
    }

    /// <summary>
    /// 所属代理组件 id
    /// </summary>
    internal string ProxyId => _proxyId;

    /// <summary>
    /// 机器人密钥
    /// </summary>
    internal string BotSecret => _data.AppSecret ?? "";

    /// <summary>
    /// 实例 uuid
    /// </summary>
    internal Guid Uuid => _uuid;

    /// <inheritdoc/>
    public void Init(string path, Guid uuid)
    {
        _configFile = Path.Combine(path, "config.json");
        _quota = new ActiveQuota(path);
        _uuid = uuid;
        Logger = LogManager.GetLogger($"QQBot[{uuid.ToString()[0..8]}]");
        Logger.Info(ProxyLang.GetLang(ProxyLangKey.InstanceInit));
    }

    /// <inheritdoc/>
    public bool Load()
    {
        Logger.Info(ProxyLang.GetLang(ProxyLangKey.StartLoad));

        return ReadConfig();
    }

    /// <inheritdoc/>
    public void Reload()
    {
        Logger.Info(ProxyLang.GetLang(ProxyLangKey.Reload));

        ReadConfig();
    }

    /// <inheritdoc/>
    public void RegisterEventHook(IRegister register)
    {
        // 收到消息后交给插件
        _dispatch = register.RegisterProxyEvent();

        // 回复先走发送句柄，拦截器放行才真正发
        _send = register.RegisterSend();


        // WebHook 模式把回调挂到宿主上，WebHost 模式自己监听端口
        register.RegisterHttpPath(HandleCallback);

        // 每天各群已发的主动消息条数，面板上是只读键值状态
        _quotaState = register.RegisterKvState("quota", "主动配额", "今天各群已经发出去的主动消息条数，每群每天上限 1000 条");
        PublishQuota();
    }

    /// <summary>
    /// 处理平台回调，实例没启用时不响应
    /// </summary>
    private Task HandleCallback(HttpContext ctx)
    {
        if (_proxy is null)
        {
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Task.CompletedTask;
        }

        return CallbackHandler.HandleAsync(ctx, this);
    }

    /// <summary>
    /// 把收到的消息交给插件，返回要回复的消息
    /// </summary>
    /// <param name="message">收到的消息</param>
    internal List<BaseMessage> Dispatch(ProxyMessage message)
    {
        return (_dispatch ??= new ProxyEventHandle()).Dispatch(message);
    }

    /// <summary>
    /// 发一条回复：先过拦截器，再真正发到平台
    /// </summary>
    /// <param name="context">消息上下文</param>
    /// <param name="reply">要发的消息</param>
    internal Task Send(ProxyMessage context, BaseMessage reply)
    {
        return _send is null ? SendAsync(context, reply) : _send.SendAsync(context, reply);
    }

    /// <inheritdoc/>
    public bool Enable()
    {
        Logger.Info(ProxyLang.GetLang(ProxyLangKey.Enable));

        if (string.IsNullOrWhiteSpace(_data.AppId)
            || string.IsNullOrWhiteSpace(_data.AppSecret))
        {
            Logger.Warn(ProxyLang.GetLang(ProxyLangKey.BotInfoEmpty));

            return false;
        }

        GetToken().GetAwaiter().GetResult();

        // 没拿到凭证也先把回调服务器起来：平台至少能完成回调地址校验，凭证补上后重启实例就能发消息
        if (_token == null)
        {
            Logger.Error(ProxyLang.GetLang(ProxyLangKey.TokenFail, _data.AppId, "unavailable"));

            if (_data.Type == HookType.WebSocket)
            {
                return false;
            }
        }

        switch (_data.Type)
        {
            case HookType.WebHook:
                _proxy = new HttpHookProxy(this);
                break;
            case HookType.WebHost:
                if (_data.Port == 0)
                {
                    Logger.Warn(ProxyLang.GetLang(ProxyLangKey.HttpStartError));
                    return false;
                }

                var host = new HttpHostProxy(this);

                if (!host.Start(_data.Port, _data.Certificate, _data.CertificatePassword))
                {
                    return false;
                }

                _proxy = host;
                break;
            case HookType.WebSocket:
                break;
        }

        _proxy?.SetToken(_token);

        return true;
    }

    /// <summary>
    /// 回复一条消息
    /// </summary>
    /// <param name="message">收到的消息，用来取回复目标和被动回复 id</param>
    /// <param name="reply">要回复的消息</param>
    public async Task SendAsync(ProxyMessage message, BaseMessage reply)
    {
        if (_proxy is null || string.IsNullOrEmpty(_token))
        {
            Logger.Warn(ProxyLang.GetLang(ProxyLangKey.NotReady));
            return;
        }

        // 一条 QQ 消息只能有一个 msg_type，列表逐条发，每条都要有自己的 msg_seq
        if (reply is ListMessage list)
        {
            foreach (var item in list.Messages)
            {
                if (item is ReplyMessage)
                {
                    continue;
                }

                await SendAsync(message, item);
            }

            return;
        }

        var scene = message.SenderType == MessageSenderType.User ? MessageScene.User : MessageScene.Group;
        var target = string.IsNullOrEmpty(message.Target) ? message.Sender : message.Target;
        var replyId = message.ReplyId;
        var state = string.IsNullOrEmpty(replyId) ? null : _replyStates.GetOrAdd(replyId, _ => new ReplyState());

        if (state is { Blocked: true })
        {
            Logger.Debug(ProxyLang.GetLang(ProxyLangKey.SendBlocked, target));

            return;
        }

        if (state is not null)
        {
            state.Last = DateTime.Now;
            PruneReplyState();
        }

        var sequence = NextSeq(replyId);

        // 被动回复有条数上限，平台是 5 条，配置里可以调小
        var passive = state is not { Active: true } && sequence <= Math.Clamp(_data.PassiveLimit, 1, 50);

        // 主动消息每个群每天有 1000 条上限，超了就别再打接口了
        if (!passive && !CanSendActive(target))
        {
            if (state is not null)
            {
                state.Blocked = true;
            }

            Logger.Warn(ProxyLang.GetLang(ProxyLangKey.ActiveQuota, target, ActiveQuota.Limit));

            return;
        }

        Logger.Debug($"发送 {target} seq={sequence} {(passive ? "被动" : "主动")} {reply.Type}");

        try
        {
            var result = await _proxy.Send(target, scene, reply, passive ? replyId : null, sequence);

            if (result.Ok)
            {
                if (!passive)
                {
                    if (state is not null)
                    {
                        state.Active = true;
                    }

                    _quota?.Add(target);
                    PublishQuota();
                }

                return;
            }

            // 被动回复失败，且是次数 / 时间用完
            if (passive && result.Code == QQApi.PassiveLimitCode)
            {
                Logger.Warn(ProxyLang.GetLang(ProxyLangKey.PassiveLimit, target));

                if (!_data.ActiveFallback)
                {
                    if (state is not null)
                    {
                        state.Blocked = true;
                    }

                    return;
                }

                if (state is not null)
                {
                    state.Active = true;
                }

                // 这一条改主动重发一次，同样受每日配额限制
                if (!CanSendActive(target))
                {
                    if (state is not null)
                    {
                        state.Blocked = true;
                    }

                    Logger.Warn(ProxyLang.GetLang(ProxyLangKey.ActiveQuota, target, ActiveQuota.Limit));

                    return;
                }

                var retry = await _proxy.Send(target, scene, reply, null, sequence);

                if (retry.Ok)
                {
                    _quota?.Add(target);
                    PublishQuota();

                    return;
                }

                if (state is not null)
                {
                    state.Blocked = true;
                }

                Logger.Warn(ProxyLang.GetLang(ProxyLangKey.ActiveFail, target, retry.Code));

                return;
            }

            // 主动发送也失败：没权限 / 没配额，后续不再发
            if (!passive)
            {
                if (state is not null)
                {
                    state.Blocked = true;
                }

                Logger.Warn(ProxyLang.GetLang(ProxyLangKey.ActiveFail, target, result.Code));

                return;
            }

            Logger.Warn(ProxyLang.GetLang(ProxyLangKey.SendFail, target));
        }
        catch (NotSupportedException)
        {
            Logger.Debug(ProxyLang.GetLang(ProxyLangKey.SendFail, target));
        }
        catch (Exception e)
        {
            Logger.Error(e, ProxyLang.GetLang(ProxyLangKey.SendError, target));
        }
    }

    /// <summary>
    /// 把各群今天已发的主动消息条数同步到状态，面板上直接看
    /// </summary>
    private void PublishQuota()
    {
        if (_quotaState is null || _quota is null)
        {
            return;
        }

        _quotaState.Clear();

        foreach (var (group, count) in _quota.Snapshot())
        {
            _quotaState.Set(group, count);
        }
    }

    /// <summary>
    /// 今天这个群还能不能发主动消息
    /// </summary>
    /// <param name="group">群 openid</param>
    private bool CanSendActive(string group)
    {
        return string.IsNullOrEmpty(group) || _quota?.CanSend(group) != false;
    }

    /// <summary>
    /// 回复状态表太大时清掉过期的
    /// </summary>
    private void PruneReplyState()
    {
        if (_replyStates.Count < ReplySeqLimit)
        {
            return;
        }

        var limit = DateTime.Now.AddMinutes(-ReplySeqLifetime);

        foreach (var item in _replyStates)
        {
            if (item.Value.Last < limit)
            {
                _replyStates.TryRemove(item.Key, out _);
            }
        }
    }

    /// <summary>
    /// 证书下拉的选项：空 + Certs 目录里的证书，当前值不在里面时补上
    /// </summary>
    private static List<string> CertOptions(string current)
    {
        var options = new List<string> { "" };
        options.AddRange(CertStore.Names());

        if (!string.IsNullOrEmpty(current) && !options.Contains(current, StringComparer.OrdinalIgnoreCase))
        {
            options.Add(current);
        }

        return options;
    }

    /// <summary>
    /// 取下一条回复序号，同一条消息的多次回复从 1 开始递增
    /// </summary>
    /// <param name="messageId">被动回复的消息 id</param>
    internal int NextSeq(string? messageId)
    {
        if (string.IsNullOrEmpty(messageId))
        {
            return 0;
        }

        PruneReplySeq();

        var item = _replySeqs.AddOrUpdate(messageId,
            _ => new ReplySeq(1, DateTime.Now),
            (_, old) => old with { Seq = old.Seq + 1 });

        return item.Seq;
    }

    /// <summary>
    /// 被动回复窗口只有几分钟，数量多的时候清一批过期的
    /// </summary>
    private void PruneReplySeq()
    {
        if (_replySeqs.Count < ReplySeqLimit)
        {
            return;
        }

        var limit = DateTime.Now.AddMinutes(-ReplySeqLifetime);

        foreach (var pair in _replySeqs)
        {
            if (pair.Value.Created < limit)
            {
                _replySeqs.TryRemove(pair.Key, out _);
            }
        }
    }

    /// <inheritdoc/>
    public bool Disable()
    {
        Logger.Info(ProxyLang.GetLang(ProxyLangKey.Disable));

        _proxy?.SetToken(null);
        _proxy?.Stop();
        _proxy = null;
        _replySeqs.Clear();

        return true;
    }

    /// <inheritdoc/>
    public bool Unload()
    {
        _proxy?.SetToken(null);
        _proxy?.Stop();
        _proxy = null;
        ComponentApi.UnregisterHttpPath(this);
        _replySeqs.Clear();

        return true;
    }

    /// <inheritdoc/>
    public Dictionary<string, ConfigMap> ConfigMap()
    {
        return new()
        {
            ["AppId"] = new ConfigStringMap
            {
                Key = "AppId",
                Value = _data.AppId ?? "",
                Description = "QQ 机器人开放平台的应用 ID，在机器人资料页可以查到",
                Placeholder = "102000001"
            },
            ["AppSecret"] = new ConfigStringMap
            {
                Key = "AppSecret",
                Value = _data.AppSecret ?? "",
                Secret = true,
                Description = "应用密钥，回调地址验签和获取 access_token 都用它",
                Placeholder = "应用密钥"
            },
            ["Port"] = new ConfigIntMap
            {
                Key = "Port",
                Value = _data.Port == 0 ? 10000 : _data.Port,
                Min = 1,
                Max = 65535,
                Default = 10000,
                Description = "WebHost 模式自己起 http 服务器时监听的端口，WebHook 模式用不到",
                Placeholder = "10000"
            },
            ["Type"] = new ConfigEnumMap
            {
                Key = "Type",
                Value = _data.Type.ToString(),
                Default = nameof(HookType.WebHook),
                Options = [.. Enum.GetNames<HookType>()],
                Description = "WebHook 注册到宿主的 /api/qq/{实例uuid}；WebHost 自己监听 Port；WebSocket 还没实现"
            },
            ["Certificate"] = new ConfigEnumMap
            {
                Key = "Certificate",
                Value = _data.Certificate,
                Default = "",
                Options = CertOptions(_data.Certificate),
                Description = "从 Certs 目录里选证书，填了 WebHost 模式就走 https；空表示不用"
            },
            ["CertificatePassword"] = new ConfigStringMap
            {
                Key = "CertificatePassword",
                Value = _data.CertificatePassword,
                Secret = true,
                Description = "pfx 的密码，导出证书时设置的",
                Placeholder = "证书密码"
            },
            ["ActiveFallback"] = new ConfigBoolMap
            {
                Key = "ActiveFallback",
                Value = _data.ActiveFallback,
                Default = false,
                Description = "被动回复超限时改成主动发送；官方 2025-04-21 已下线主动推送，开了也只会在失败后停止发送"
            },
            ["PassiveLimit"] = new ConfigIntMap
            {
                Key = "PassiveLimit",
                Value = _data.PassiveLimit <= 0 ? 5 : _data.PassiveLimit,
                Min = 1,
                Max = 50,
                Default = 5,
                Description = "同一条消息最多被动回复几次，官方限制是 5 次（群聊 5 分钟、单聊 60 分钟内有效）",
                Placeholder = "5"
            }
        };
    }

    /// <inheritdoc/>
    public void UpdateMap(Dictionary<string, ConfigMap> data)
    {
        if (data.TryGetValue("AppId", out var appId) && appId is ConfigStringMap appIdMap)
        {
            _data.AppId = appIdMap.Value ?? "";
        }

        if (data.TryGetValue("AppSecret", out var secret) && secret is ConfigStringMap secretMap)
        {
            _data.AppSecret = secretMap.Value ?? "";
        }

        if (data.TryGetValue("Port", out var port) && port is ConfigIntMap portMap)
        {
            _data.Port = (ushort)Math.Clamp(portMap.Value, 1, 65535);
        }

        if (data.TryGetValue("Type", out var type) && type is ConfigEnumMap typeMap
            && Enum.TryParse(typeMap.Value, true, out HookType hook))
        {
            _data.Type = hook;
        }

        if (data.TryGetValue("Certificate", out var certificate) && certificate is ConfigEnumMap certificateMap)
        {
            _data.Certificate = certificateMap.Value ?? "";
        }

        if (data.TryGetValue("CertificatePassword", out var certificatePassword) && certificatePassword is ConfigStringMap certificatePasswordMap)
        {
            _data.CertificatePassword = certificatePasswordMap.Value ?? "";
        }

        if (data.TryGetValue("ActiveFallback", out var activeFallback) && activeFallback is ConfigBoolMap activeFallbackMap)
        {
            _data.ActiveFallback = activeFallbackMap.Value;
        }

        if (data.TryGetValue("PassiveLimit", out var passiveLimit) && passiveLimit is ConfigIntMap passiveLimitMap)
        {
            _data.PassiveLimit = Math.Clamp(passiveLimitMap.Value, 1, 50);
        }

        SaveConfig();
    }

    /// <inheritdoc/>
    public void Tick()
    {
        // 每天第一秒把主动配额清零，别等有人发消息才发现跨天了
        if (_quota?.RolloverIfNeeded() == true)
        {
            Logger.Info("主动消息配额已跨天清零");

            PublishQuota();
        }

        if (_update)
        {
            return;
        }

        // 凭证还没拿到时按退避时间重试，别每秒都打一次平台
        if (_token == null && DateTime.Now < _tokenRetryAt)
        {
            return;
        }

        var time = DateTime.Now;

        var pass = time - _lastDateTime;
        _expires -= pass.TotalSeconds;
        if (_expires <= ProxyMain.TokenRefreshAhead)
        {
            _update = true;
            Task.Run(GetToken);
        }
    }

    /// <summary>
    /// 读取实例配置，失败时用空配置
    /// </summary>
    /// <returns>是否读取成功</returns>
    private bool ReadConfig()
    {
        try
        {
            if (File.Exists(_configFile))
            {
                var obj = JsonUtils.ToObj(File.ReadAllText(_configFile), JsonContext.Default.InstanceDataObj);
                if (obj != null)
                {
                    Logger.Info(ProxyLang.GetLang(ProxyLangKey.ConfigRead));
                    _data = obj;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, ProxyLang.GetLang(ProxyLangKey.ConfigReadError));
        }

        _data = new InstanceDataObj();

        return false;
    }

    /// <summary>
    /// 保存实例配置到 Instances/{uuid}/config.json
    /// </summary>
    private void SaveConfig()
    {
        if (string.IsNullOrEmpty(_configFile))
        {
            return;
        }

        try
        {
            File.WriteAllText(_configFile, JsonUtils.ToString(_data, JsonContext.Default.InstanceDataObj));
            Logger.Info(ProxyLang.GetLang(ProxyLangKey.ConfigSave));
        }
        catch (Exception e)
        {
            Logger.Error(e, ProxyLang.GetLang(ProxyLangKey.ConfigSaveError));
        }
    }

    /// <summary>
    /// 获取凭证
    /// </summary>
    /// <returns>是否成功获取</returns>
    private async Task GetToken()
    {
        _token = null;

        var obj = new GetAppAccessTokenObj()
        {
            AppId = _data.AppId,
            Clientecret = _data.AppSecret
        };

        try
        {
            var data = await QQApi.GetToken(obj);
            // code 缺省 / 0 都算成功，只有带了错误码才算失败
            if (data == null || string.IsNullOrWhiteSpace(data.AccessToken) || (data.Code ?? 0) != 0)
            {
                Logger.Error(ProxyLang.GetLang(ProxyLangKey.TokenFail, data?.Code, data?.Message));

                _tokenRetryAt = DateTime.Now.AddSeconds(30);
            }
            else
            {
                _token = data.AccessToken;
                _expires = long.Parse(data.ExpiresIn ?? "0");
                _lastDateTime = DateTime.Now;
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, ProxyLang.GetLang(ProxyLangKey.TokenError));
        }

        _proxy?.SetToken(_token);
        _update = false;
    }
}
