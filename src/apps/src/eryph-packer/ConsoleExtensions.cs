using System.CommandLine;

namespace Eryph.Packer;

internal static class ConsoleExtensions
{
    internal static void SetTerminalForegroundRed(this IConsole console)
    {
        if (Platform.IsConsoleRedirectionCheckSupported &&
            !Console.IsOutputRedirected)
        {
            Console.ForegroundColor = ConsoleColor.Red;
        }
        else if (Platform.IsConsoleRedirectionCheckSupported)
        {
            Console.ForegroundColor = ConsoleColor.Red;
        }
    }

    internal static void ResetTerminalForegroundColor(this IConsole console)
    {
        if (Platform.IsConsoleRedirectionCheckSupported &&
            !Console.IsOutputRedirected)
        {
            Console.ResetColor();
        }
        else if (Platform.IsConsoleRedirectionCheckSupported)
        {
            Console.ResetColor();
        }
    }
}