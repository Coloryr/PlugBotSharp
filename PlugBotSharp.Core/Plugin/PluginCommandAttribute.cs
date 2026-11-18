using System;
using System.Collections.Generic;
using System.Text;

namespace PlugBotSharp.Core.Plugin;

[AttributeUsage(AttributeTargets.Class)]
public class PluginCommandAttribute(string? basecommand = null) : Attribute
{
    public string? BaseCommand { get; init; } = basecommand;
}

[AttributeUsage(AttributeTargets.Class)]
public class CommandAttribute(string command) : Attribute
{
    public string Command { get; init; } = command;
}