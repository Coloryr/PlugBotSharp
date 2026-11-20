using System;
using System.Collections.Generic;
using System.Text;

namespace PlugBotSharp.Core.Plugin;

public interface IBasePlugin
{
    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="path">配置文件夹路径</param>
    /// <returns></returns>
    public bool Init(string path);
    /// <summary>
    /// 加载
    /// </summary>
    /// <returns></returns>
    public bool Load();
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
}
