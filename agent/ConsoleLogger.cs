using MelonLoader.Installer.Core;

namespace LemonAgent;

internal sealed class ConsoleLogger : IPatchLogger
{
    public void Log(string message) => Console.WriteLine(message);
}
