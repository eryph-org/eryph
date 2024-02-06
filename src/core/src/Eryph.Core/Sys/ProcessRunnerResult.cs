namespace Eryph.Core.Sys;

public class ProcessRunnerResult
{
    public ProcessRunnerResult(int exitCode, string output)
    {
        ExitCode = exitCode;
        Output = output;
    }

    public int ExitCode { get; init; }

    public string Output { get; init; }
}
