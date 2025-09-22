using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Eryph.AnsiConsole.JsonLines;

public class JsonLineResult : JsonLineOutput
{
    public bool Successful { get; set; }

    public int ExitCode { get; set; }

    public JsonElement? Result { get; set; }

    public string? Error { get; set; }
}
