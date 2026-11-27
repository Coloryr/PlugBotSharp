namespace PlugBotSharp.Api;

public interface IBaseProxy : IComponent
{
    /// <summary>
    /// 注册 / 订阅事件的阶段，过了这个阶段注册器就关闭
    /// </summary>
    /// <param name="register">注册器</param>
    public void RegisterEventHook(IRegister register);
    /// <summary>
    /// 创建实例
    /// </summary>
    /// <returns></returns>
    public IProxyInstasnce CreateInstance();
    /// <summary>
    /// 实例重载配置
    /// </summary>
    /// <param name="instasnce"></param>
    public void Reload(IProxyInstasnce instasnce);
}
