namespace Eryph.Core.Sys;

public class ProcessRunnerResult(int exitCode, string output)
{
    public int ExitCode { get; init; } = exitCode;

    public string Output { get; init; } = output;
}
