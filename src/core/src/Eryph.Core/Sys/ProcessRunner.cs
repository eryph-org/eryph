using LanguageExt;

namespace Eryph.Core.Sys;

public static class ProcessRunner<RT>
    where RT : struct, HasProcessRunner<RT>
{
    public static Aff<RT, ProcessRunnerResult> runProcess(
        string executablePath,
        string arguments,
        string workingDirectory = "",
        bool includeStandardError = false) =>
        default(RT).ProcessRunnerEff.MapAsync(e => e.RunProcess(
            executablePath, arguments, workingDirectory, includeStandardError));
}
