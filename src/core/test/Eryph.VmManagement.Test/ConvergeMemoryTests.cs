using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.VmManagement.Converging;
using Eryph.VmManagement.Data.Full;
using FluentAssertions;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Test;

public class ConvergeMemoryTests
{
    private readonly ConvergeFixture _fixture = new();
    private AssertCommand? _executedCommand;

    public ConvergeMemoryTests()
    {
        _fixture.Engine.RunCallback = cmd =>
        {
            _executedCommand = cmd;
            return unit;
        };
    }

    [Fact]
    public async Task Converge_NoMemoryConfiguration_UsesDefaultMemory()
    {
        const int startupMb = 1024;
        _fixture.Config.Memory = null;
        var vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo
        {
            DynamicMemoryEnabled = true,
            MemoryStartup = 4096 * 1024L * 1024,
            MemoryMinimum = 42 * 1024L * 1024,
            MemoryMaximum = 42 * 1024L * 1024,
        });

        var convergeTask = new ConvergeMemory(_fixture.Context);
        await convergeTask.Converge(vmInfo);

        _executedCommand.Should().NotBeNull();
        _executedCommand!.ShouldBeCommand("Set-VMMemory")
            .ShouldBeParam("VM", vmInfo.PsObject)
            .ShouldBeParam("DynamicMemoryEnabled", false)
            .ShouldBeParam("StartupBytes", EryphConstants.DefaultCatletMemoryMb * 1024L * 1024)
            .ShouldBeComplete();
    }

    [Fact]
    public async Task Converge_OnlyStartupMemoryConfigured_DisablesDynamicMemory()
    {
        const int startupMb = 2048;
        _fixture.Config.Memory = new CatletMemoryConfig
        {
            Startup = startupMb,
        };
        var vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo
        {
            DynamicMemoryEnabled = true,
            MemoryStartup = 42 * 1024L * 1024,
            MemoryMinimum = 42 * 1024L * 1024,
            MemoryMaximum = 42 * 1024L * 1024,
        });

        var convergeTask = new ConvergeMemory(_fixture.Context);
        await convergeTask.Converge(vmInfo);

        _executedCommand.Should().NotBeNull();
        _executedCommand!.ShouldBeCommand("Set-VMMemory")
            .ShouldBeParam("VM", vmInfo.PsObject)
            .ShouldBeParam("DynamicMemoryEnabled", false)
            .ShouldBeParam("StartupBytes", startupMb * 1024L * 1024)
            .ShouldBeComplete();
    }

    [Fact]
    public async Task Converge_NoMinimumMemoryConfigured_UsesStartupMemoryAsMinimum()
    {
        const int startupMb = 2048;
        const int maximumMb = 4096;
        _fixture.Config.Memory = new CatletMemoryConfig
        {
            Startup = startupMb,
            Maximum = maximumMb,
        };
        var vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo
        {
            DynamicMemoryEnabled = false,
            MemoryStartup = 42 * 1024L * 1024,
            MemoryMinimum = 42 * 1024L * 1024,
            MemoryMaximum = 42 * 1024L * 1024,
        });

        var convergeTask = new ConvergeMemory(_fixture.Context);
        await convergeTask.Converge(vmInfo);

        _executedCommand.Should().NotBeNull();
        _executedCommand!.ShouldBeCommand("Set-VMMemory")
            .ShouldBeParam("VM", vmInfo.PsObject)
            .ShouldBeParam("DynamicMemoryEnabled", true)
            .ShouldBeParam("StartupBytes", startupMb * 1024L * 1024)
            .ShouldBeParam("MinimumBytes", startupMb * 1024L * 1024)
            .ShouldBeParam("MinimumBytes", maximumMb * 1024L * 1024)
            .ShouldBeComplete();
    }

    [Fact]
    public async Task Converge_NoMaximumMemoryConfigured_UsesHyperVDefaultAsMaximum()
    {
        const int startupMb = 2048;
        const int minimumMb = 1024;
        _fixture.Config.Memory = new CatletMemoryConfig
        {
            Startup = startupMb,
            Minimum = minimumMb,
        };
        var vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo
        {
            DynamicMemoryEnabled = false,
            MemoryStartup = 42 * 1024L * 1024,
            MemoryMinimum = 42 * 1024L * 1024,
            MemoryMaximum = 42 * 1024L * 1024,
        });

        var convergeTask = new ConvergeMemory(_fixture.Context);
        await convergeTask.Converge(vmInfo);

        _executedCommand.Should().NotBeNull();
        _executedCommand!.ShouldBeCommand("Set-VMMemory")
            .ShouldBeParam("VM", vmInfo.PsObject)
            .ShouldBeParam("DynamicMemoryEnabled", true)
            .ShouldBeParam("StartupBytes", startupMb * 1024L * 1024)
            .ShouldBeParam("MinimumBytes", minimumMb * 1024L * 1024)
            // The Hyper-V default is 1 TiB
            .ShouldBeParam("MinimumBytes", 1 * 1024L * 1024 * 1024 * 1024)
            .ShouldBeComplete();
    }

    [Fact]
    public async Task Converge_NoChanges_CommandIsNotExecuted()
    {
        _fixture.Config.Memory = new CatletMemoryConfig
        {
            Startup = 2048,
            Minimum = 1024,
            Maximum = 4096
        };
        var vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo
        {
            DynamicMemoryEnabled = true,
            MemoryStartup = 2048 * 1024L * 1024,
            MemoryMinimum = 1024 * 1024L * 1024,
            MemoryMaximum = 4096 * 1024L * 1024,
        });

        var convergeTask = new ConvergeMemory(_fixture.Context);
        await convergeTask.Converge(vmInfo);

        _executedCommand.Should().BeNull();
    }
}