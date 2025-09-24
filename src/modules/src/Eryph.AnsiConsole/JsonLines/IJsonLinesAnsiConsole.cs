using Spectre.Console;

namespace Eryph.AnsiConsole.JsonLines;

internal interface IJsonLinesAnsiConsole : IAnsiConsole
{
    void WriteResult(string? result);

    void WriteError(int code, string message);
}
