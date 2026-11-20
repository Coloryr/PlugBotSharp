using PlugBotSharp.Core;

namespace PlugBotSharp.Run;

internal class Program
{
    static void Main(string[] args)
    {
        BotManager.Init(AppContext.BaseDirectory);
        BotManager.Load();
        BotManager.Stop();
    }
}
