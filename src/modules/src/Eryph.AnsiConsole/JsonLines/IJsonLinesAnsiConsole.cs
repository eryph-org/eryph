using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Spectre.Console;

namespace Eryph.AnsiConsole.JsonLines;

internal interface IJsonLinesAnsiConsole : IAnsiConsole
{
    void WriteResult(JsonElement? result);

    void WriteError(int code, string message);
}
