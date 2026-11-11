using System;
using System.Collections.Generic;
using System.Text;

namespace PlugBotSharp.Core.Plugin;

[AttributeUsage(AttributeTargets.Class)]
public class PluginCommandAttribute : Attribute
{
    public string? BaseCommand { get; init; }

    public PluginCommandAttribute(string? basecommand = null)
    { 
        BaseCommand = basecommand;
    }
}

[AttributeUsage(AttributeTargets.Class)]
public class CommandAttribute : Attribute
{
    public string Command { get; init; }

    public CommandAttribute(string command) 
    {
        Command = command;
    }   
}