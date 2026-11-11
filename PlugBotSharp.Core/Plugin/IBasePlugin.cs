using System;
using System.Collections.Generic;
using System.Text;

namespace PlugBotSharp.Core.Plugin;

public interface IBasePlugin
{
    public string Id { get; }
    public string Name { get; }

    public string Description { get; }

    public bool Init(string path);
    public bool Load();
    public bool Unload();
    public bool Enable();
    public bool Disable();
}
