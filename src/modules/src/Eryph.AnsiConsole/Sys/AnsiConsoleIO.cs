using Spectre.Console;

namespace Eryph.AnsiConsole.Sys;

public interface AnsiConsoleIO
{
    public IAnsiConsole AnsiConsole { get; }
}

public readonly struct LiveAnsiConsoleIO(IAnsiConsole ansiConsole) : AnsiConsoleIO
{
    public static readonly AnsiConsoleIO Default = new LiveAnsiConsoleIO(Spectre.Console.AnsiConsole.Console);

    public IAnsiConsole AnsiConsole => ansiConsole;
}
