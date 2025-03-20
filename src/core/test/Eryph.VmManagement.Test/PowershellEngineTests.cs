using FluentAssertions;
using FluentAssertions.LanguageExt;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Eryph.VmManagement.Test;

public sealed class PowershellEngineTests : IDisposable
{
    // TODO add tests for all methods
    private readonly PowershellEngine _engine = new(NullLogger.Instance);

    public PowershellEngineTests()
    {
        Environment.SetEnvironmentVariable("ERYPH_UNITTEST_A", "a");
        Environment.SetEnvironmentVariable("ERYPH_UNITTEST_B", "b");
    }

    [Fact]
    public async Task GetObjectsAsync_ItemsExist_ReturnsValues()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_*")
            .AddCommand("Sort-Object")
            .AddParameter("Property", "Name");

        var result = await _engine.GetObjectsAsync<EnvVar>(command);
        result.Should().BeRight().Which.Should().SatisfyRespectively(
            var =>
            {
                var.Value.Key.Should().Be("ERYPH_UNITTEST_A");
                var.Value.Value.Should().Be("a");
            },
            var =>
            {
                var.Value.Key.Should().Be("ERYPH_UNITTEST_B");
                var.Value.Value.Should().Be("b");
            });
    }

    [Fact]
    public async Task GetObjectsAsync_ItemDoesNotExist_ReturnsEmpty()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_MISSING")
            .AddCommand("Sort-Object")
            .AddParameter("Property", "Name");

        var result = await _engine.GetObjectsAsync<EnvVar>(command);
        result.Should().BeRight().Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetObjectsAsync_ItemDoesNotExistAndScriptError_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_*")
            .AddCommand("Sort-Object")
            .AddParameter("Property", "Name")
            .Script("throw test-error");

        var result = await _engine.GetObjectsAsync<EnvVar>(command);
        var failure = result.Should().BeLeft().Subject;
        failure.Message.Should().Contain("test-error");
        failure.Category.Should().Be(PowershellFailureCategory.Other);
    }

    [Fact]
    public async Task GetObjectsAsync_IsCancelled_AbortsEarly()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Start-Sleep")
            .AddParameter("Second", 50);

        var start = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await _engine.GetObjectsAsync<EnvVar>(command, null, cts.Token);

        start.Should().BeWithin(TimeSpan.FromSeconds(6)).Before(DateTimeOffset.Now);
        var failure = result.Should().BeLeft().Subject;
        failure.Message.Should().Be("The Powershell pipeline has been cancelled.");
        failure.Category.Should().Be(PowershellFailureCategory.PipelineStopped);
    }

    [Fact]
    public async Task GetObjectsAsync_WithProgress_ReportsProgress()
    {
        var command = PsCommandBuilder.Create()
            .Script("""
                    Write-Progress -Activity Testing -PercentComplete 25
                    Write-Progress -Activity Testing -PercentComplete 50
                    Write-Progress -Activity Testing -PercentComplete 75
                    Get-Item -Path Env:\ERYPH_UNITTEST_*
                    """);

        var progress = new List<int>();

        var result = await _engine.GetObjectsAsync<EnvVar>(
            command,
            p => { progress.Add(p); return Task.CompletedTask; },
            CancellationToken.None);

        result.Should().BeRight().Which.Should().HaveCount(2);
        progress.Should().Equal(25, 50, 75);
    }


    [Fact]
    public async Task GetObjectValue_ValueExists_ReturnsValue()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\OS")
            .AddCommand("Select-Object")
            .AddParameter("ExpandProperty", "Value");

        var result = await _engine.GetObjectValueAsync<string>(command);
        result.Should().BeRight()
            .Which.Should().BeSome()
            .Which.Should().Be("Windows_NT");
    }

    [Fact]
    public async Task GetObjectValue_NotFound_ReturnsNone()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", $@"Env:\test-{Guid.NewGuid()}")
            //.AddParameter("ErrorAction", "Stop")
            .AddCommand("Select-Object")
            .AddParameter("ExpandProperty", "Value");

        var result = await _engine.GetObjectValueAsync<string>(command);
        result.Should().BeRight().Which.Should().BeNone();
    }

    [Fact]
    public async Task GetObjectValue_NotFoundAndMissingCommand_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", $@"Env:\test-{Guid.NewGuid()}")
            .AddCommand("Test-Missing");

        var result = await _engine.GetObjectValueAsync<string>(command);
        result.Should().BeLeft();
    }

    [Fact]
    public async Task GetObjectValue_NotFoundAndScriptError_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", $@"Env:\test-{Guid.NewGuid()}")
            .AddParameter("ErrorAction", "Continue")
            .Script("throw test-error");

        var result = await _engine.GetObjectValueAsync<string>(command);
        result.Should().BeLeft();
    }

    [Fact]
    public async Task RunAsync_IsCancelled_AbortsEarly()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Start-Sleep")
            .AddParameter("Second", 50);
        
        var start = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _engine.GetObjectsAsync<string>(command, null, cts.Token);
        
        start.Should().BeWithin(TimeSpan.FromSeconds(2)).Before(DateTimeOffset.Now);
        result.Should().BeLeft();
    }

    [Fact]
    public async Task RunAsync_WithProgress_ReportsProgress()
    {
        var command = PsCommandBuilder.Create()
            .Script("""
                    Write-Progress -Activity Testing -PercentComplete 25
                    Write-Progress -Activity Testing -PercentComplete 50
                    Write-Progress -Activity Testing -PercentComplete 75
                    """);

        var progress = new List<int>();

        var result = await _engine.RunAsync(
            command,
            p => { progress.Add(p); return Task.CompletedTask; },
            CancellationToken.None);

        result.Should().BeRight();
        progress.Should().Equal(25, 50, 75);
    }

    public void Dispose()
    {
        _engine.Dispose();
        Environment.SetEnvironmentVariable("ERYPH_UNITTEST_A", null);
        Environment.SetEnvironmentVariable("ERYPH_UNITTEST_B", null);
    }

    private class EnvVar
    {
        public string Key { get; set; }

        public string Value { get; set; }
    }
}
