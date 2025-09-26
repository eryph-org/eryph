namespace Eryph.AnsiConsole.JsonLines;

internal class JsonLineError : JsonLineOutput
{
    public required int ExitCode { get; set; }

    public required string Error { get; set; }
}
