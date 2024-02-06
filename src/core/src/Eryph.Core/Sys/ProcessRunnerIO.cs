using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Eryph.Core.Sys;

public interface ProcessRunnerIO
{
    ValueTask<ProcessRunnerResult> RunProcess(
        string executablePath,
        string arguments,
        string workingDirectory = "",
        bool includeStandardError = false);
}

public readonly struct LiveProcessRunnerIO : ProcessRunnerIO
{
    public static readonly ProcessRunnerIO Default = new LiveProcessRunnerIO();

    public async ValueTask<ProcessRunnerResult> RunProcess(
        string executablePath,
        string arguments,
        string workingDirectory = "",
        bool includeStandardError = false)
    {
        var processStartInfo = new ProcessStartInfo(executablePath, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        using var process = Process.Start(processStartInfo);
        if (process is null)
            return new ProcessRunnerResult(-1, "The process could not be started");

        // Read both outputs in parallel. This is necessary to avoid deadlocks.
        var outputs = await Task.WhenAll(process.StandardOutput.ReadToEndAsync(),
            process.StandardError.ReadToEndAsync());

        var output = includeStandardError
            ? string.Join(Environment.NewLine, outputs.Where(s => !string.IsNullOrWhiteSpace(s)))
            : outputs[0];

        await process.WaitForExitAsync().ConfigureAwait(false);

        return new ProcessRunnerResult(process.ExitCode, output);
    }
}
