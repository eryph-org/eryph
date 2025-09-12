using FluentAssertions;
using FluentAssertions.LanguageExt;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Eryph.VmManagement.HyperV.Test;

/// <summary>
/// These tests verify the behavior of the <see cref="PowershellEngine"/>.
/// Hence, the Powershell commands are actually executed. To avoid side effects,
/// the tests access environment variables which are created specifically for
/// these tests.
/// </summary>
public sealed class PowershellEngineTests : IDisposable
{
    private readonly PowershellEngineLock _engineLock = new();
    private readonly PowershellEngine _engine;

    public PowershellEngineTests()
    {
        _engine = new PowershellEngine(NullLogger.Instance, _engineLock);
        Environment.SetEnvironmentVariable("ERYPH_UNITTEST_A", "a");
        Environment.SetEnvironmentVariable("ERYPH_UNITTEST_B", "b");
    }

    [Fact]
    public async Task GetObjectAsync_ItemsExist_ReturnsValues()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_A");

        var result = await _engine.GetObjectAsync<EnvVar>(command);
        var entry = result.Should().BeRight().Which.Should().BeSome().Subject;
        entry.Value.Key.Should().Be("ERYPH_UNITTEST_A");
        entry.Value.Value.Should().Be("a");
    }

    [Fact]
    public async Task GetObjectAsync_ItemDoesNotExist_ReturnsEmpty()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_MISSING");

        var result = await _engine.GetObjectAsync<EnvVar>(command);
        result.Should().BeRight().Which.Should().BeNone();
    }

    [Fact]
    public async Task GetObjectAsync_ItemDoesNotExistAndMissingCommand_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_A")
            .AddCommand("Test-Missing");

        var result = await _engine.GetObjectAsync<EnvVar>(command);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Contain("Test-Missing");
        error.Category.Should().Be(PowershellErrorCategory.CommandNotFound);
    }

    [Fact]
    public async Task GetObjectAsync_ScriptError_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .Script("throw 'test-error'");

        var result = await _engine.GetObjectAsync<EnvVar>(command);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Contain("test-error");
        error.Category.Should().Be(PowershellErrorCategory.OperationStopped);
    }

    [Fact]
    public async Task GetObjectAsync_IsCancelled_AbortsEarly()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Start-Sleep")
            .AddParameter("Second", 60);

        var start = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _engine.GetObjectAsync<EnvVar>(command, cancellationToken: cts.Token);

        start.Should().BeWithin(TimeSpan.FromSeconds(30)).Before(DateTimeOffset.Now);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Be("The pipeline has been stopped.");
        error.Category.Should().Be(PowershellErrorCategory.PipelineStopped);
    }

    [Fact]
    public async Task GetObjectAsync_LockNotAcquired_AbortsEarly()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_A");

        await _engineLock.AcquireLockAsync();

        var start = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _engine.GetObjectAsync<EnvVar>(command, cancellationToken: cts.Token);

        start.Should().BeWithin(TimeSpan.FromSeconds(30)).Before(DateTimeOffset.Now);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Be("The operation has been cancelled before the global lock could be acquired.");
        error.Category.Should().Be(PowershellErrorCategory.PipelineStopped);
    }

    [Fact]
    public async Task GetObjectAsync_WithProgress_ReportsProgress()
    {
        var command = PsCommandBuilder.Create()
            .Script("""
                    Write-Progress -Activity Testing -PercentComplete 25
                    Write-Progress -Activity Testing -PercentComplete 50
                    Write-Progress -Activity Testing -PercentComplete 75
                    Get-Item -Path Env:\ERYPH_UNITTEST_A
                    """);

        var progress = new List<int>();

        var result = await _engine.GetObjectAsync<EnvVar>(
            command,
            p => { progress.Add(p); return Task.CompletedTask; });

        result.Should().BeRight().Which.Should().BeSome();
        progress.Should().Equal(25, 50, 75);
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
    public async Task GetObjectsAsync_ItemDoesNotExistAndMissingCommand_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_*")
            .AddCommand("Sort-Object")
            .AddParameter("Property", "Name")
            .AddCommand("Test-Missing");

        var result = await _engine.GetObjectsAsync<EnvVar>(command);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Contain("Test-Missing");
        error.Category.Should().Be(PowershellErrorCategory.CommandNotFound);
    }

    [Fact]
    public async Task GetObjectsAsync_ScriptError_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .Script("throw 'test-error'");

        var result = await _engine.GetObjectsAsync<EnvVar>(command);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Contain("test-error");
        error.Category.Should().Be(PowershellErrorCategory.OperationStopped);
    }

    [Fact]
    public async Task GetObjectsAsync_IsCancelled_AbortsEarly()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Start-Sleep")
            .AddParameter("Second", 60);

        var start = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _engine.GetObjectsAsync<EnvVar>(command, cancellationToken: cts.Token);

        start.Should().BeWithin(TimeSpan.FromSeconds(30)).Before(DateTimeOffset.Now);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Be("The pipeline has been stopped.");
        error.Category.Should().Be(PowershellErrorCategory.PipelineStopped);
    }

    [Fact]
    public async Task GetObjectsAsync_LockNotAcquired_AbortsEarly()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_*")
            .AddCommand("Sort-Object")
            .AddParameter("Property", "Name");

        await _engineLock.AcquireLockAsync();

        var start = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _engine.GetObjectsAsync<EnvVar>(command, cancellationToken: cts.Token);
        
        start.Should().BeWithin(TimeSpan.FromSeconds(30)).Before(DateTimeOffset.Now);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Be("The operation has been cancelled before the global lock could be acquired.");
        error.Category.Should().Be(PowershellErrorCategory.PipelineStopped);
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
            p => { progress.Add(p); return Task.CompletedTask; });

        result.Should().BeRight().Which.Should().HaveCount(2);
        progress.Should().Equal(25, 50, 75);
    }

    [Fact]
    public async Task GetObjectValueAsync_ItemsExist_ReturnsValues()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_A")
            .AddCommand("Select-Object")
            .AddParameter("ExpandProperty", "Value");

        var result = await _engine.GetObjectValueAsync<string>(command);
        result.Should().BeRight().Which.Should().BeSome()
            .Which.Should().Be("a");
    }

    [Fact]
    public async Task GetObjectValueAsync_ItemDoesNotExist_ReturnsEmpty()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_MISSING");

        var result = await _engine.GetObjectValueAsync<string>(command);
        result.Should().BeRight().Which.Should().BeNone();
    }

    [Fact]
    public async Task GetObjectValueAsync_ItemDoesNotExistAndMissingCommand_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_A")
            .AddCommand("Test-Missing");

        var result = await _engine.GetObjectValueAsync<string>(command);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Contain("Test-Missing");
        error.Category.Should().Be(PowershellErrorCategory.CommandNotFound);
    }

    [Fact]
    public async Task GetObjectValueAsync_ScriptError_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .Script("throw 'test-error'");

        var result = await _engine.GetObjectValueAsync<string>(command);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Contain("test-error");
        error.Category.Should().Be(PowershellErrorCategory.OperationStopped);
    }

    [Fact]
    public async Task GetObjectValueAsync_IsCancelled_AbortsEarly()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Start-Sleep")
            .AddParameter("Second", 60);

        var start = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _engine.GetObjectValueAsync<EnvVar>(command, cancellationToken: cts.Token);

        start.Should().BeWithin(TimeSpan.FromSeconds(30)).Before(DateTimeOffset.Now);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Be("The pipeline has been stopped.");
        error.Category.Should().Be(PowershellErrorCategory.PipelineStopped);
    }

    [Fact]
    public async Task GetObjectValueAsync_LockNotAcquired_AbortsEarly()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_A")
            .AddCommand("Select-Object")
            .AddParameter("ExpandProperty", "Value");

        await _engineLock.AcquireLockAsync();

        var start = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _engine.GetObjectValueAsync<string>(command, cancellationToken: cts.Token);
        
        start.Should().BeWithin(TimeSpan.FromSeconds(30)).Before(DateTimeOffset.Now);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Be("The operation has been cancelled before the global lock could be acquired.");
        error.Category.Should().Be(PowershellErrorCategory.PipelineStopped);
    }

    [Fact]
    public async Task GetObjectValueAsync_WithProgress_ReportsProgress()
    {
        var command = PsCommandBuilder.Create()
            .Script("""
                    Write-Progress -Activity Testing -PercentComplete 25
                    Write-Progress -Activity Testing -PercentComplete 50
                    Write-Progress -Activity Testing -PercentComplete 75
                    Get-Item -Path Env:\ERYPH_UNITTEST_A
                    """);

        var progress = new List<int>();

        var result = await _engine.GetObjectValueAsync<string>(
            command,
            p => { progress.Add(p); return Task.CompletedTask; });

        result.Should().BeRight().Which.Should().BeSome();
        progress.Should().Equal(25, 50, 75);
    }

    [Fact]
    public async Task GetObjectValuesAsync_ItemsExist_ReturnsValues()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_*")
            .AddCommand("Sort-Object")
            .AddParameter("Property", "Name")
            .AddCommand("Select-Object")
            .AddParameter("ExpandProperty", "Value");

        var result = await _engine.GetObjectValuesAsync<string>(command);
        result.Should().BeRight().Which.Should().Equal("a", "b");
    }

    [Fact]
    public async Task GetObjectValuesAsync_ItemDoesNotExist_ReturnsEmpty()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_MISSING")
            .AddCommand("Sort-Object")
            .AddParameter("Property", "Name")
            .AddCommand("Select-Object")
            .AddParameter("ExpandProperty", "Value");

        var result = await _engine.GetObjectValuesAsync<string>(command);
        result.Should().BeRight().Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetObjectValuesAsync_ItemDoesNotExistAndMissingCommand_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_*")
            .AddCommand("Sort-Object")
            .AddParameter("Property", "Name")
            .AddCommand("Select-Object")
            .AddParameter("ExpandProperty", "Value")
            .AddCommand("Test-Missing");

        var result = await _engine.GetObjectValuesAsync<string>(command);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Contain("Test-Missing");
        error.Category.Should().Be(PowershellErrorCategory.CommandNotFound);
    }

    [Fact]
    public async Task GetObjectValuesAsync_ScriptError_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .Script("throw 'test-error'");

        var result = await _engine.GetObjectValuesAsync<string>(command);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Contain("test-error");
        error.Category.Should().Be(PowershellErrorCategory.OperationStopped);
    }

    [Fact]
    public async Task GetObjectValuesAsync_IsCancelled_AbortsEarly()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Start-Sleep")
            .AddParameter("Second", 60);

        var start = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _engine.GetObjectValuesAsync<string>(command, cancellationToken: cts.Token);

        start.Should().BeWithin(TimeSpan.FromSeconds(30)).Before(DateTimeOffset.Now);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Be("The pipeline has been stopped.");
        error.Category.Should().Be(PowershellErrorCategory.PipelineStopped);
    }

    [Fact]
    public async Task GetObjectValuesAsync_LockNotAcquired_AbortsEarly()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_*")
            .AddCommand("Sort-Object")
            .AddParameter("Property", "Name")
            .AddCommand("Select-Object")
            .AddParameter("ExpandProperty", "Value");

        await _engineLock.AcquireLockAsync();

        var start = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _engine.GetObjectValuesAsync<string>(command, cancellationToken: cts.Token);
        
        start.Should().BeWithin(TimeSpan.FromSeconds(30)).Before(DateTimeOffset.Now);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Be("The operation has been cancelled before the global lock could be acquired.");
        error.Category.Should().Be(PowershellErrorCategory.PipelineStopped);
    }

    [Fact]
    public async Task GetObjectValuesAsync_WithProgress_ReportsProgress()
    {
        var command = PsCommandBuilder.Create()
            .Script("""
                    Write-Progress -Activity Testing -PercentComplete 25
                    Write-Progress -Activity Testing -PercentComplete 50
                    Write-Progress -Activity Testing -PercentComplete 75
                    Get-Item -Path Env:\ERYPH_UNITTEST_* | Sort-Object | Select-Object -ExpandProperty Value
                    """);

        var progress = new List<int>();

        var result = await _engine.GetObjectValuesAsync<string>(
            command,
            p => { progress.Add(p); return Task.CompletedTask; });

        result.Should().BeRight().Which.Should().Equal("a", "b");
        progress.Should().Equal(25, 50, 75);
    }

    [Fact]
    public async Task RunAsync_ItemsExist_ReturnsUnit()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_A");

        var result = await _engine.RunAsync(command);
        result.Should().BeRight();
    }

    [Fact]
    public async Task RunAsync_ItemDoesNotExist_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_MISSING");

        var result = await _engine.RunAsync(command);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Category.Should().Be(PowershellErrorCategory.ObjectNotFound);
    }

    [Fact]
    public async Task RunAsync_ItemDoesNotExistAndMissingCommand_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_A")
            .AddCommand("Test-Missing");

        var result = await _engine.RunAsync(command);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Contain("Test-Missing");
        error.Category.Should().Be(PowershellErrorCategory.CommandNotFound);
    }

    [Fact]
    public async Task RunAsync_ScriptError_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .Script("throw 'test-error'");

        var result = await _engine.RunAsync(command);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Contain("test-error");
        error.Category.Should().Be(PowershellErrorCategory.OperationStopped);
    }

    [Fact]
    public async Task RunAsync_IsCancelled_AbortsEarly()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Start-Sleep")
            .AddParameter("Second", 60);

        var start = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _engine.RunAsync(command, cancellationToken: cts.Token);

        start.Should().BeWithin(TimeSpan.FromSeconds(30)).Before(DateTimeOffset.Now);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Be("The pipeline has been stopped.");
        error.Category.Should().Be(PowershellErrorCategory.PipelineStopped);
    }

    [Fact]
    public async Task RunAsync_LockNotAcquired_AbortsEarly()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_A");

        await _engineLock.AcquireLockAsync();

        var start = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _engine.RunAsync(command, cancellationToken: cts.Token);

        start.Should().BeWithin(TimeSpan.FromSeconds(30)).Before(DateTimeOffset.Now);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Be("The operation has been cancelled before the global lock could be acquired.");
        error.Category.Should().Be(PowershellErrorCategory.PipelineStopped);
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
            p => { progress.Add(p); return Task.CompletedTask; });

        result.Should().BeRight();
        progress.Should().Equal(25, 50, 75);
    }

    [Fact]
    public async Task RunOutOfProcessAsync_ItemsExist_ReturnsUnit()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_A");

        var result = await _engine.RunOutOfProcessAsync(command);
        result.Should().BeRight();
    }

    [Fact]
    public async Task RunOutOfProcessAsync_ItemDoesNotExist_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_MISSING");

        var result = await _engine.RunOutOfProcessAsync(command);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Category.Should().Be(PowershellErrorCategory.ObjectNotFound);
    }

    [Fact]
    public async Task RunOutOfProcessAsync_ItemDoesNotExistAndMissingCommand_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_A")
            .AddCommand("Test-Missing");

        var result = await _engine.RunOutOfProcessAsync(command);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Contain("Test-Missing");
        error.Category.Should().Be(PowershellErrorCategory.CommandNotFound);
    }

    [Fact]
    public async Task RunOutOfProcessAsync_ScriptError_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .Script("throw 'test-error'");

        var result = await _engine.RunOutOfProcessAsync(command);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Contain("test-error");
        error.Category.Should().Be(PowershellErrorCategory.OperationStopped);
    }

    [Fact]
    public async Task RunOutOfProcessAsync_IsCancelled_AbortsEarly()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Start-Sleep")
            .AddParameter("Second", 60);

        var start = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var result = await _engine.RunOutOfProcessAsync(command, cancellationToken: cts.Token);

        start.Should().BeWithin(TimeSpan.FromSeconds(30)).Before(DateTimeOffset.Now);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Be("The pipeline has been stopped.");
        error.Category.Should().Be(PowershellErrorCategory.PipelineStopped);
    }

    [Fact]
    public async Task RunOutOfProcessAsync_LockNotAcquired_AbortsEarly()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("Path", @"Env:\ERYPH_UNITTEST_A");

        await _engineLock.AcquireLockAsync();

        var start = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var result = await _engine.RunOutOfProcessAsync(command, cancellationToken: cts.Token);

        start.Should().BeWithin(TimeSpan.FromSeconds(30)).Before(DateTimeOffset.Now);
        var error = result.Should().BeLeft().Which.Should().BeOfType<PowershellError>().Subject;
        error.Message.Should().Be("The operation has been cancelled before the global lock could be acquired.");
        error.Category.Should().Be(PowershellErrorCategory.PipelineStopped);
    }

    [Fact]
    public async Task RunOutOfProcessAsync_WithProgress_ReportsProgress()
    {
        var command = PsCommandBuilder.Create()
            .Script("""
                    Write-Progress -Activity Testing -PercentComplete 25
                    Write-Progress -Activity Testing -PercentComplete 50
                    Write-Progress -Activity Testing -PercentComplete 75
                    """);

        var progress = new List<int>();

        var result = await _engine.RunOutOfProcessAsync(
            command,
            p => { progress.Add(p); return Task.CompletedTask; });

        result.Should().BeRight();
        progress.Should().Equal(25, 50, 75);
    }

    public void Dispose()
    {
        _engine.Dispose();
        _engineLock.Dispose();
        Environment.SetEnvironmentVariable("ERYPH_UNITTEST_A", null);
        Environment.SetEnvironmentVariable("ERYPH_UNITTEST_B", null);
    }

    private class EnvVar
    {
        public string? Key { get; set; }

        public string? Value { get; set; }
    }
}
