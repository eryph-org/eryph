using Spectre.Console;

namespace Eryph.AnsiConsole.Sys;

public interface AnsiConsoleIO
{
    public IAnsiConsole AnsiConsole { get; }
}

public readonly struct LiveAnsiConsoleIO : AnsiConsoleIO
{
    public static readonly AnsiConsoleIO Default = new LiveAnsiConsoleIO();

    public IAnsiConsole AnsiConsole => Spectre.Console.AnsiConsole.Console;
}
