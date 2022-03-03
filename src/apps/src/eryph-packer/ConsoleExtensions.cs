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

internal static class Platform
{
    private static bool? _isConsoleRedirectionCheckSupported;

    public static bool IsConsoleRedirectionCheckSupported
    {
        get
        {
            if (_isConsoleRedirectionCheckSupported is null)
            {
                try
                {
                    var check = Console.IsOutputRedirected;
                    _isConsoleRedirectionCheckSupported = true;
                }

                catch (PlatformNotSupportedException)
                {
                    _isConsoleRedirectionCheckSupported = false;
                }
            }

            return _isConsoleRedirectionCheckSupported.Value;
        }
    }
}