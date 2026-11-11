using System;
using System.Collections.Generic;
using System.Text;

namespace PlugBotSharp.Core.Proxy;

public abstract class BaseProxyInstasnce
{
    public abstract string Name { get; }
    
    public abstract void Init(string path);
}
