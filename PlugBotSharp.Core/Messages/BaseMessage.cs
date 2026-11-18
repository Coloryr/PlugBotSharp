using System;
using System.Collections.Generic;
using System.Text;

namespace PlugBotSharp.Core.Messages;

public enum MessageType
{ 
    Text, Image, List, File
}

public enum MessageSender
{ 
    User, Group
}

public abstract class BaseMessage
{
    public MessageType Type { get; set; }
    public string Sender { get; set; }
}
