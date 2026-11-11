using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace PlugBotSharp.Core.Configs;

internal class ConfigObj
{

}

internal class InstanceObj
{ 
    
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ConfigObj))]
internal partial class SourceGenerationContext : JsonSerializerContext
{

}