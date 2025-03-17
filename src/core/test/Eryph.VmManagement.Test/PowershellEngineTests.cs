using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            .AddParameter("LiteralPath", @"Env:\OS")
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
            .AddParameter("LiteralPath", $@"Env:\test-{Guid.NewGuid()}")
            .AddCommand("Select-Object")
            .AddParameter("ExpandProperty", "Value");

        var result = await _engine.GetObjectValueAsync<string>(command);
        result.Should().BeRight().Which.Should().BeNone();
    }

    [Fact]
    public async Task GetObjectValue_NotFoundAndOtherError_ReturnsError()
    {
        var command = PsCommandBuilder.Create()
            .AddCommand("Get-Item")
            .AddParameter("LiteralPath", $@"Env:\test-{Guid.NewGuid()}")
            .AddCommand("throw test-error");

        var result = await _engine.GetObjectValueAsync<string>(command);
        result.Should().BeLeft();
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}

