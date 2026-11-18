using System;
using System.Collections.Generic;
using System.Text;

namespace PlugBotSharp.Core.Plugin;

internal class PluginAssembly : BaseAssembly
{
    public IBasePlugin Plugin { get; private set; }

    // public readonly List<>
    public readonly List<PluginAssembly> Deps = [];
}
