namespace Eryph.AnsiConsole.JsonLines;

internal class JsonLineResult : JsonLineOutput
{
    public int ExitCode { get; set; }

    public string? Result { get; set; }
}
