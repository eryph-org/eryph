using Eryph.Core.Sys;
using LanguageExt;

namespace Eryph.Core.Tests.Sys;

/// <summary>
/// These are integration tests for the <see cref="ProcessRunner{RT}"/> which
/// start real Powershell processes. These tests will only work on Windows.
/// </summary>
public class ProcessRunnerTests
{
    [Fact]
    public async Task RunProcess_ProcessHasNonZeroExitCode_ReturnsNonZeroExitCode()
    {
        var result = await Run(ProcessRunner<TestRuntime>.runProcess(
            "powershell.exe", @"-C ""exit 1"""));
        
        result.Should().BeSuccess().Which.ExitCode.Should().Be(1);
    }

    [Fact]
    public async Task RunProcess_ProcessHasStandardOutput_ReturnsStandardOutput()
    {

        var result = await Run(ProcessRunner<TestRuntime>.runProcess(
            "powershell.exe", @"-C ""Write-Output 'my test'"";"));

        var processResult = result.Should().BeSuccess().Subject;
        processResult.ExitCode.Should().Be(0);
        processResult.Output.Should().Be("my test\r\n");
    }

    [Fact]
    public async Task RunProcess_ProcessHasStandardErrorAndErrorShouldBeIncluded_ReturnsStandardError()
    {

        var result = await Run(ProcessRunner<TestRuntime>.runProcess(
            "powershell.exe", @"-C ""Write-Output 'test output'; [System.Console]::Error.WriteLine('test error')""", "", true));

        var processResult = result.Should().BeSuccess().Subject;
        processResult.ExitCode.Should().Be(0);
        processResult.Output.Should().Be("test output\r\n\r\ntest error\r\n");
    }
    
    [Fact]
    public async Task RunProcess_ProcessHasStandardErrorAndErrorShouldNotBeIncluded_DoesNotReturnStandardError()
    {

        var result = await Run(ProcessRunner<TestRuntime>.runProcess(
            "powershell.exe", @"-C ""Write-Output 'test output'; [System.Console]::Error.WriteLine('test error')""", "", false));

        var processResult = result.Should().BeSuccess().Subject;
        processResult.ExitCode.Should().Be(0);
        processResult.Output.Should().Be("test output\r\n");
    }

    private async ValueTask<Fin<ProcessRunnerResult>> Run(Aff<TestRuntime, ProcessRunnerResult> logic)
    {
        using var cts = new CancellationTokenSource();
        return await logic.Run(new TestRuntime(cts));
    }

    private readonly struct TestRuntime : HasProcessRunner<TestRuntime>
    {
        private readonly CancellationTokenSource _cancellationTokenSource;

        public TestRuntime(CancellationTokenSource cancellationTokenSource)
        {
            _cancellationTokenSource = cancellationTokenSource;
        }

        public TestRuntime LocalCancel => new(new CancellationTokenSource());

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public CancellationTokenSource CancellationTokenSource => _cancellationTokenSource;
        
        public Eff<TestRuntime, ProcessRunnerIO> ProcessRunnerEff => Prelude.SuccessEff(LiveProcessRunnerIO.Default);
    }
}
