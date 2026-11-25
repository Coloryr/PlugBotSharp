using System;
using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.Core;

public interface IBaseComponent
{
    /// <summary>
    /// 加载
    /// </summary>
    /// <returns></returns>
    public bool Load();
    /// <summary>
    /// 重载配置
    /// </summary>
    public void Reload();
    /// <summary>
    /// 卸载
    /// </summary>
    /// <returns></returns>
    public bool Unload();
    /// <summary>
    /// 启用
    /// </summary>
    /// <returns></returns>
    public bool Enable();
    /// <summary>
    /// 禁用
    /// </summary>
    /// <returns></returns>
    public bool Disable();
    /// <summary>
    /// 获取配置设置，传递给网页端的配置
    /// </summary>
    public Dictionary<string, ConfigMap> ConfigMap();
    /// <summary>
    /// 更新配置
    /// </summary>
    /// <param name="data"></param>
    public void UpdateMap(Dictionary<string, ConfigMap> data);
}

public interface IComponent : IBaseComponent
{
    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="path">配置文件夹路径</param>
    /// <returns></returns>
    public bool Init(string path);
}
