using PlugBotSharp.Core;

namespace PlugBotSharp;

internal class Program
{
    static void Main(string[] args)
    {
        BotManager.Init(AppContext.BaseDirectory);
        BotManager.Load();
        BotManager.Stop();
    }
}
