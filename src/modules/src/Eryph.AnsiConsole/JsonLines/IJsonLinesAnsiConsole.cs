using Spectre.Console;

namespace Eryph.AnsiConsole.JsonLines;

public interface IJsonLinesAnsiConsole : IAnsiConsole
{
    void WriteResult(string? result);

    void WriteError(int code, string message);
}
