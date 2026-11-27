using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PlugBotSharp.Core.Auth;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core.Http;

/// <summary>
/// 登录、初始账户、账户管理和会话管理接口
/// </summary>
internal static class AuthApi
{
    /// <summary>
    /// 请求上下文里缓存当前会话
    /// </summary>
    private const string SessionKey = "pbs.session";

    /// <summary>
    /// 注册 /api/auth 和 /api/admin/accounts、/api/admin/sessions
    /// </summary>
    /// <param name="app">asp 应用</param>
    public static void Map(WebApplication app)
    {
        var auth = app.MapGroup("/api/auth");

        auth.MapGet("/state", State);
        auth.MapPost("/login", (Delegate)Login);
        auth.MapPost("/init", (Delegate)Init);
        auth.MapPost("/logout", Logout);

        var admin = app.MapGroup("/api/admin");

        admin.MapGet("/accounts", Accounts);
        admin.MapPost("/accounts", (Delegate)AddAccount);
        admin.MapPost("/accounts/{id}/password", (Delegate)ChangePassword);
        admin.MapDelete("/accounts/{id}", RemoveAccount);

        admin.MapGet("/sessions", Sessions);
        admin.MapDelete("/sessions", RemoveOtherSessions);
        admin.MapDelete("/sessions/{id}", RemoveSession);
    }

    /// <summary>
    /// 校验登录 cookie，成功时返回会话，并缓存到请求上下文
    /// </summary>
    /// <param name="ctx">请求上下文</param>
    public static SessionObj? Authenticate(HttpContext ctx)
    {
        if (ctx.Items.TryGetValue(SessionKey, out var cached) && cached is SessionObj cachedSession)
        {
            return cachedSession;
        }

        var token = ctx.Request.Cookies[SessionManager.CookieName];

        if (!SessionManager.TryValidate(token, out var session, out var renewed) || session is null)
        {
            if (!string.IsNullOrEmpty(token))
            {
                ctx.Response.Cookies.Delete(SessionManager.CookieName, new CookieOptions { Path = "/" });
            }

            return null;
        }

        // 快过期了，重写一次 cookie 延长有效期
        if (renewed)
        {
            SetCookie(ctx, token!);
        }

        ctx.Items[SessionKey] = session;
        return session;
    }

    /// <summary>
    /// 当前登录的会话和账户
    /// </summary>
    /// <param name="ctx">请求上下文</param>
    private static (SessionObj Session, AccountObj Account)? Current(HttpContext ctx)
    {
        var session = Authenticate(ctx);

        if (session is null)
        {
            return null;
        }

        var account = AccountManager.Find(session.AccountId);

        if (account is null)
        {
            // 账户被删了，会话也要清掉
            SessionManager.RemoveById(session.Id);
            return null;
        }

        return (session, account);
    }

    /// <summary>
    /// 面板状态：是否已经初始化、是否已登录
    /// </summary>
    private static IResult State(HttpContext ctx)
    {
        var current = Current(ctx);

        return ApiUtil.Ok(new
        {
            initialized = AccountManager.IsInitialized,
            authenticated = current is not null,
            user = current is null ? null : ToUser(current.Value.Account)
        });
    }

    /// <summary>
    /// 创建第一个账户，只在没有任何账户时可用，创建后直接登录
    /// </summary>
    private static async Task<IResult> Init(HttpContext ctx)
    {
        if (AccountManager.IsInitialized)
        {
            return ApiUtil.Fail(BotLang.GetLang(BotLangKey.AuthAlreadyInit), StatusCodes.Status409Conflict);
        }

        var obj = await ApiUtil.Read(ctx, SourceGenerationContext.Default.InitAccountReqObj);

        if (obj is null)
        {
            return ApiUtil.Fail(BotLang.GetLang(BotLangKey.AuthUserInvalid));
        }

        if (!AccountManager.TryCreateInitial(obj.User, obj.Password, out var account, out var error))
        {
            return ApiUtil.Fail(error);
        }

        // 初始化完直接登录，顺便记一次登录时间
        AccountManager.TouchLogin(account!);

        var token = SessionManager.Create(account!.Id, GetIp(ctx), GetAgent(ctx));
        SetCookie(ctx, token);

        BotManager.Logger?.Info(BotLang.GetLang(BotLangKey.AuthInitDone));

        return ApiUtil.Ok(new { user = ToUser(account) }, BotLang.GetLang(BotLangKey.AuthInitDone));
    }

    /// <summary>
    /// 登录，失败次数过多会被临时锁定
    /// </summary>
    private static async Task<IResult> Login(HttpContext ctx)
    {
        var obj = await ApiUtil.Read(ctx, SourceGenerationContext.Default.LoginReqObj);

        if (obj is null || string.IsNullOrWhiteSpace(obj.User))
        {
            return ApiUtil.Fail(BotLang.GetLang(BotLangKey.AuthLoginFail), StatusCodes.Status401Unauthorized);
        }

        var ip = GetIp(ctx);
        var user = obj.User.Trim();

        if (!LoginGuard.Check(user, ip, out var retryAfter))
        {
            ctx.Response.Headers.RetryAfter = retryAfter.ToString();

            return ApiUtil.Fail(BotLang.GetLang(BotLangKey.AuthLocked, retryAfter), StatusCodes.Status429TooManyRequests);
        }

        if (!AccountManager.Verify(user, obj.Password ?? "", out var account))
        {
            LoginGuard.Fail(user, ip);

            // 失败也慢一点，降低爆破速度
            await Task.Delay(400);

            return ApiUtil.Fail(BotLang.GetLang(BotLangKey.AuthLoginFail), StatusCodes.Status401Unauthorized);
        }

        LoginGuard.Reset(user, ip);
        AccountManager.TouchLogin(account!);

        var token = SessionManager.Create(account!.Id, ip, GetAgent(ctx));
        SetCookie(ctx, token);

        return ApiUtil.Ok(new { user = ToUser(account) }, BotLang.GetLang(BotLangKey.AuthLoginDone));
    }

    /// <summary>
    /// 退出登录，撤销当前会话
    /// </summary>
    private static IResult Logout(HttpContext ctx)
    {
        SessionManager.Remove(ctx.Request.Cookies[SessionManager.CookieName]);

        ctx.Response.Cookies.Delete(SessionManager.CookieName, new CookieOptions { Path = "/" });
        ctx.Items.Remove(SessionKey);

        return ApiUtil.Ok(null, BotLang.GetLang(BotLangKey.AuthLogoutDone));
    }

    /// <summary>
    /// 账户列表
    /// </summary>
    private static IResult Accounts(HttpContext ctx)
    {
        var current = Current(ctx);

        if (current is null)
        {
            return ApiUtil.Fail(BotLang.GetLang(BotLangKey.AuthRequired), StatusCodes.Status401Unauthorized);
        }

        return ApiUtil.Ok(AccountManager.List().Select(x => new
        {
            id = x.Id,
            user = x.User,
            created = Format(x.Created),
            lastLogin = Format(x.LastLogin),
            sessions = SessionManager.Count(x.Id),
            self = x.Id == current.Value.Account.Id
        }).ToList());
    }

    /// <summary>
    /// 添加账户
    /// </summary>
    private static async Task<IResult> AddAccount(HttpContext ctx)
    {
        var obj = await ApiUtil.Read(ctx, SourceGenerationContext.Default.AddAccountReqObj);

        if (obj is null)
        {
            return ApiUtil.Fail(BotLang.GetLang(BotLangKey.AuthUserInvalid));
        }

        if (!AccountManager.TryAdd(obj.User, obj.Password, out var account, out var error) || account == null)
        {
            return ApiUtil.Fail(error);
        }

        return ApiUtil.Ok(new { account = ToUser(account) }, BotLang.GetLang(BotLangKey.AuthAccountAdded, account!.User));
    }

    /// <summary>
    /// 修改密码，OldPassword 为空表示管理员重置
    /// </summary>
    private static async Task<IResult> ChangePassword(HttpContext ctx, string id)
    {
        var obj = await ApiUtil.Read(ctx, SourceGenerationContext.Default.ChangePasswordReqObj);

        if (obj is null)
        {
            return ApiUtil.Fail(BotLang.GetLang(BotLangKey.AuthPasswordWeak));
        }

        var current = Current(ctx);

        if (!AccountManager.TryChangePassword(id, obj.OldPassword, obj.Password, out var error))
        {
            return ApiUtil.Fail(error);
        }

        // 改密后踢掉这个账户的其他会话，自己改自己时保留当前会话
        SessionManager.RemoveAccount(id, current?.Session.AccountId == id ? current?.Session.Id : null);

        var user = AccountManager.Find(id)?.User ?? id;

        return ApiUtil.Ok(null, BotLang.GetLang(BotLangKey.AuthPasswordChanged, user));
    }

    /// <summary>
    /// 删除账户，不能删自己，也不能删到只剩零个
    /// </summary>
    private static IResult RemoveAccount(HttpContext ctx, string id)
    {
        var current = Current(ctx);

        if (current?.Account.Id == id)
        {
            return ApiUtil.Fail(BotLang.GetLang(BotLangKey.AuthCannotDeleteSelf));
        }

        var user = AccountManager.Find(id)?.User ?? id;

        if (!AccountManager.TryRemove(id, out var error))
        {
            return ApiUtil.Fail(error);
        }

        SessionManager.RemoveAccount(id);

        return ApiUtil.Ok(null, BotLang.GetLang(BotLangKey.AuthAccountDeleted, user));
    }

    /// <summary>
    /// 登录会话列表，供面板管理 cookie
    /// </summary>
    private static IResult Sessions(HttpContext ctx)
    {
        var current = Current(ctx);

        if (current is null)
        {
            return ApiUtil.Fail(BotLang.GetLang(BotLangKey.AuthRequired), StatusCodes.Status401Unauthorized);
        }

        return ApiUtil.Ok(SessionManager.List().Select(x => new
        {
            id = x.Id,
            accountId = x.AccountId,
            user = AccountManager.Find(x.AccountId)?.User ?? "-",
            ip = x.Ip,
            agent = x.Agent,
            created = Format(x.Created.ToLocalTime()),
            lastSeen = Format(x.LastSeen.ToLocalTime()),
            expires = Format(x.Expires.ToLocalTime()),
            current = x.Id == current.Value.Session.Id
        }).ToList());
    }

    /// <summary>
    /// 撤销某个会话
    /// </summary>
    private static IResult RemoveSession(HttpContext ctx, string id)
    {
        var current = Current(ctx);

        if (!SessionManager.RemoveById(id))
        {
            return ApiUtil.Fail(BotLang.GetLang(BotLangKey.AuthSessionNotFound), StatusCodes.Status404NotFound);
        }

        // 把自己的会话撤掉了，顺便清 cookie
        if (current?.Session.Id == id)
        {
            ctx.Response.Cookies.Delete(SessionManager.CookieName, new CookieOptions { Path = "/" });
            ctx.Items.Remove(SessionKey);
        }

        return ApiUtil.Ok(null, BotLang.GetLang(BotLangKey.AuthSessionRemoved));
    }

    /// <summary>
    /// 撤销除当前会话以外的全部会话
    /// </summary>
    private static IResult RemoveOtherSessions(HttpContext ctx)
    {
        var current = Current(ctx);

        SessionManager.RemoveOthers(current?.Session.Id);

        return ApiUtil.Ok(null, BotLang.GetLang(BotLangKey.AuthSessionsCleared));
    }

    /// <summary>
    /// 写登录 cookie，HttpOnly + SameSite=Strict
    /// </summary>
    private static void SetCookie(HttpContext ctx, string token)
    {
        ctx.Response.Cookies.Append(SessionManager.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = ctx.Request.IsHttps,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.Add(SessionManager.Lifetime),
            IsEssential = true
        });
    }

    /// <summary>
    /// 输出给面板的账户结构
    /// </summary>
    private static object ToUser(AccountObj account)
    {
        return new
        {
            id = account.Id,
            user = account.User,
            created = Format(account.Created),
            lastLogin = Format(account.LastLogin)
        };
    }

    /// <summary>
    /// 时间格式化，空时间返回空串
    /// </summary>
    private static string Format(DateTime time)
    {
        return time == default ? "" : time.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// 客户端 ip
    /// </summary>
    private static string GetIp(HttpContext ctx)
    {
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "";
    }

    /// <summary>
    /// 客户端 UA
    /// </summary>
    private static string GetAgent(HttpContext ctx)
    {
        return ctx.Request.Headers.UserAgent.ToString();
    }
}
