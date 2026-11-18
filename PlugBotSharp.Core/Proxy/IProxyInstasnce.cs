using System;
using System.Collections.Generic;
using System.Text;

namespace PlugBotSharp.Core.Proxy;

public interface IProxyInstasnce
{
    public void Init(string path, Guid uuid);
    public Task<bool> Load();
    public Task<bool> Enable();
    public Task<bool> Disable();
    public void Stop();
    public Task<bool> Run();
    public void Tick();
}
