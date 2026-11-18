using System;
using System.Collections.Generic;
using System.Text;

namespace PlugBotSharp.Core.Proxy;

public interface IBaseProxy
{
    public string Id { get; }
    public string Name { get; }

    public string Description { get; }

    public bool Init(string path);
    public bool Load();
    public bool Unload();
    public IProxyInstasnce CreateInstance();
    public void ConfigInstance(IProxyInstasnce instasnce);
    public bool Enable();
    public bool Disable();
}
