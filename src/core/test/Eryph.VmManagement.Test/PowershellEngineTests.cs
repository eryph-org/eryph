using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.VmManagement.Data.Full;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Eryph.VmManagement.Test;

public sealed class PowershellEngineTests : IDisposable
{
    private readonly PowershellEngine _engine = new PowershellEngine(NullLogger.Instance);

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

    /*
    [Fact]
    public async Task GetVm_NotFound_ReturnsNone()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-VM")
            .AddParameter("Id", $"{Guid.NewGuid()}")
            //.AddParameter("ErrorAction", "Stop")
            .AddCommand("Select-Object")
            .AddParameter("ExpandProperty", "Name");

        var result = await _engine.GetObjectValueAsync<string>(command);
        result.Should().BeRight().Which.Should().BeNone();
    }

    [Fact]
    public async Task GetVm_Found_ReturnsVmInfo()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-VM")
            .AddParameter("Name", "test");

        var result = await _engine.GetObjectAsync<VirtualMachineInfo>(command);
        result.Should().BeRight().Which.Should().BeSome();
    }
    */

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

    public void Dispose()
    {
        _engine.Dispose();
    }
}

