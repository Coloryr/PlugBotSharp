using Microsoft.AspNetCore.Http;
using PlugBotSharp.Api.Events;
using PlugBotSharp.Api.Messages;

namespace PlugBotSharp.Api;

/// <summary>
/// RegisterEventHook 阶段拿到的注册器，方法分三类：
/// 注册（Register）是把自己的东西暴露给别人用，订阅（Subscribe）是接收别人的事件，
/// 最后一类是注册后返回、可以一直拿着的句柄（事件 / 状态 / 分发 / 发送）。
/// 阶段结束后注册器关闭，再调用会抛异常，但句柄照样能用。
/// </summary>
public interface IRegister
{
    // ------------------------------------------------------------------ 注册：暴露给别人

    /// <summary>
    /// 注册自定义插件事件，返回触发器。事件全名是 [组件id]事件id，别的组件按这个名字订阅
    /// </summary>
    /// <param name="eventId">事件 id，同一个组件内不能重复</param>
    /// <returns>用来触发事件的句柄</returns>
    RaiseHandle RegisterPluginEvent(string eventId);

    /// <summary>
    /// 注册 http 接口，插件 / 代理是 /api/{组件id}，实例是 /api/{组件id}/{实例uuid}
    /// </summary>
    /// <param name="handler">回调</param>
    void RegisterHttpPath(Func<HttpContext, Task> handler);

    // ------------------------------------------------------------------ 订阅：接收别人的事件

    /// <summary>
    /// 订阅消息事件，代理收到消息后调用，返回值是要回复的消息
    /// </summary>
    /// <param name="handler">回调</param>
    void SubscribeMessage(Func<ProxyMessage, List<BaseMessage>> handler);

    /// <summary>
    /// 订阅消息事件（长任务版）：回调在后台跑，不阻塞代理，处理完调 event.Reply(...) 把消息发回发起方
    /// </summary>
    /// <param name="handler">回调</param>
    void SubscribeMessage(Func<MessageEvent, Task> handler);

    /// <summary>
    /// 订阅 Proxy 事件，实例启用 / 停用时广播
    /// </summary>
    /// <param name="handler">回调</param>
    void SubscribeProxy(Action<ProxyEvent> handler);

    /// <summary>
    /// 订阅插件事件，收全部：生命周期事件和所有自定义事件
    /// </summary>
    /// <param name="handler">回调</param>
    void SubscribePlugin(Action<PluginEvent> handler);

    /// <summary>
    /// 订阅指定名字的自定义事件，只收这个名字，收不到生命周期事件
    /// </summary>
    /// <param name="name">事件全名，[组件id]事件id，一般直接用 RaiseHandle.Name，组件 id 可以写 *</param>
    /// <param name="handler">回调</param>
    void SubscribePluginEvent(string name, Action<PluginEvent> handler);

    /// <summary>
    /// 订阅自定义事件，只收匹配的，收不到生命周期事件。
    /// 组件 id 和事件 id 都可以写 * 通配：("demo", "*") 是 demo 的全部事件，
    /// ("*", "ping") 是所有组件的 ping，("*", "*") 是所有自定义事件
    /// </summary>
    /// <param name="componentId">发出事件的组件 id，* 表示全部</param>
    /// <param name="eventId">事件 id，* 表示全部</param>
    /// <param name="handler">回调</param>
    void SubscribePluginEvent(string componentId, string eventId, Action<PluginEvent> handler);

    /// <summary>
    /// 订阅发送前的拦截器：消息真正发到平台之前调用，返回 true 表示取消这次发送。
    /// 一般是插件用来过滤 / 屏蔽消息，所有拦截器都会收到，任意一个返回 true 就取消
    /// </summary>
    /// <param name="handler">回调，返回 true 取消发送</param>
    void SubscribeSendFilter(Func<ProxyMessage, BaseMessage, Task<bool>> handler);

    // ------------------------------------------------------------------ 句柄：注册后随时用

    /// <summary>
    /// 注册一个单值状态，面板上显示成 名字: 值，返回的句柄随时更新。
    /// 值支持文本、开关和各种整数 / 小数等基础类型
    /// </summary>
    /// <param name="name">状态名，面板接口里当键用</param>
    /// <param name="label">面板上显示的名字，不填就用状态名</param>
    /// <param name="description">面板上的解释，鼠标停在标签上能看到</param>
    StateValueHandle RegisterState(string name, string label = "", string description = "");

    /// <summary>
    /// 注册一个只读键值状态，面板上显示成可展开的表格，返回的句柄随时增删键。
    /// 只读指的是面板侧不能改，插件这边随便写
    /// </summary>
    /// <param name="name">状态名，面板接口里当键用</param>
    /// <param name="label">面板上显示的名字，不填就用状态名</param>
    /// <param name="description">面板上的解释，鼠标停在标签上能看到</param>
    StateKvHandle RegisterKvState(string name, string label = "", string description = "");

    /// <summary>
    /// 注册代理事件，返回的句柄用来把收到的消息注入进来：所有订阅消息事件的组件都会收到，
    /// 返回值是它们要回复的消息。只有代理 / 实例能调用，插件调用会抛异常
    /// </summary>
    ProxyEventHandle RegisterProxyEvent();

    /// <summary>
    /// 注册发送器，插件用它把消息发给指定实例；代理 / 实例拿到的发送器不能填自己的代理 id
    /// </summary>
    SendHandle RegisterSend();
}

/// <summary>
