using System;
using System.Collections.Generic;
using System.Text;
using PlugBotSharp.Core.Messages;

namespace PlugBotSharp.Core;

public static class EventManager
{
    public static List<BaseMessage> HandleMessage(string id, Guid instance, BaseMessage message)
    {
        return [];
    }
}
