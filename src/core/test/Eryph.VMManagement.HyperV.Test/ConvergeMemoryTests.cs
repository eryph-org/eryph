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
using FluentAssertions.LanguageExt;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Test;

public class ConvergeMemoryTests
{
    private readonly ConvergeFixture _fixture = new();
    private readonly ConvergeMemory _convergeTask;
    private AssertCommand? _executedCommand;

    public ConvergeMemoryTests()
    {
        _convergeTask = new(_fixture.Context);
        _fixture.Engine.RunCallback = cmd =>
        {
            _executedCommand = cmd;
            return unit;
        };
    }

    [Fact]
    public async Task Converge_NoMemoryConfiguration_DisablesDynamicMemory()
    {
        _fixture.Config.Memory = null;
        var vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo
        {
            DynamicMemoryEnabled = true,
            MemoryStartup = 42 * 1024L * 1024,
            MemoryMinimum = 42 * 1024L * 1024,
            MemoryMaximum = 42 * 1024L * 1024,
        });

        var result = await _convergeTask.Converge(vmInfo);
        result.Should().BeRight();

        _executedCommand.Should().NotBeNull();
        _executedCommand!.ShouldBeCommand("Set-VMMemory")
            .ShouldBeParam("VM", vmInfo.PsObject)
            .ShouldBeParam("DynamicMemoryEnabled", false)
            .ShouldBeComplete();
    }

    [Fact]
    public async Task Converge_MinimumMemoryIsConfigured_EnablesDynamicMemory()
    {
        _fixture.Config.Memory = new CatletMemoryConfig()
        {
            Minimum = 1024,
            Startup = 2048,
        };
        var vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo
        {
            DynamicMemoryEnabled = false,
            MemoryStartup = 2048 * 1024L * 1024,
            MemoryMinimum = 512 * 1024L * 1024,
            MemoryMaximum = 2048 * 1024L * 1024,
        });

        var result = await _convergeTask.Converge(vmInfo);
        result.Should().BeRight();

        _executedCommand.Should().NotBeNull();
        _executedCommand!.ShouldBeCommand("Set-VMMemory")
            .ShouldBeParam("VM", vmInfo.PsObject)
            .ShouldBeParam("DynamicMemoryEnabled", true)
            .ShouldBeParam("MinimumBytes", 1024 * 1024L * 1024)
            .ShouldBeComplete();
    }

    [Fact]
    public async Task Converge_MaximumMemoryIsConfigured_EnablesDynamicMemory()
    {
        _fixture.Config.Memory = new CatletMemoryConfig()
        {
            Maximum = 4096,
            Startup = 2048,
        };
        var vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo
        {
            DynamicMemoryEnabled = false,
            MemoryStartup = 2048 * 1024L * 1024,
            MemoryMinimum = 2048 * 1024L * 1024,
            MemoryMaximum = 3072 * 1024L * 1024,
        });

        var result = await _convergeTask.Converge(vmInfo);
        result.Should().BeRight();

        _executedCommand.Should().NotBeNull();
        _executedCommand!.ShouldBeCommand("Set-VMMemory")
            .ShouldBeParam("VM", vmInfo.PsObject)
            .ShouldBeParam("DynamicMemoryEnabled", true)
            .ShouldBeParam("MaximumBytes", 4096 * 1024L * 1024)
            .ShouldBeComplete();
    }

    [Fact]
    public async Task Converge_DynamicMemoryIsExplicitlyDisabled_DisablesDynamicMemory()
    {
        _fixture.Config.Capabilities =
        [
            new CatletCapabilityConfig
            {
                Name = EryphConstants.Capabilities.DynamicMemory,
                Details = [EryphConstants.CapabilityDetails.Disabled]
            },
        ];
        _fixture.Config.Memory = new CatletMemoryConfig()
        {
            Startup = 2048,
            Minimum = 512,
            Maximum = 4096,
        };
        var vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo
        {
            DynamicMemoryEnabled = true,
            MemoryStartup = 2048 * 1024L * 1024,
            MemoryMinimum = 1024 * 1024L * 1024,
            MemoryMaximum = 3072 * 1024L * 1024,
        });

        var result = await _convergeTask.Converge(vmInfo);
        result.Should().BeRight();

        _executedCommand.Should().NotBeNull();
        // MinimumBytes and MaximumBytes must not be set as Hyper-V
        // rejects them when DynamicMemoryEnabled is false.
        _executedCommand!.ShouldBeCommand("Set-VMMemory")
            .ShouldBeParam("VM", vmInfo.PsObject)
            .ShouldBeParam("DynamicMemoryEnabled", false)
            .ShouldBeComplete();
    }

    [Fact]
    public async Task Converge_MemoryIsFullyConfigured_UpdatesAllSizes()
    {
        _fixture.Config.Memory = new CatletMemoryConfig
        {
            Startup = 2048,
            Minimum = 1024,
            Maximum = 4096,
        };
        var vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo
        {
            DynamicMemoryEnabled = false,
            MemoryStartup = 42 * 1024L * 1024,
            MemoryMinimum = 42 * 1024L * 1024,
            MemoryMaximum = 42 * 1024L * 1024,
        });

        var result = await _convergeTask.Converge(vmInfo);
        result.Should().BeRight();

        _executedCommand.Should().NotBeNull();
        _executedCommand!.ShouldBeCommand("Set-VMMemory")
            .ShouldBeParam("VM", vmInfo.PsObject)
            .ShouldBeParam("DynamicMemoryEnabled", true)
            .ShouldBeParam("StartupBytes", 2048 * 1024L * 1024)
            .ShouldBeParam("MinimumBytes", 1024 * 1024L * 1024)
            .ShouldBeParam("MaximumBytes", 4096 * 1024L * 1024)
            .ShouldBeComplete();
    }

    [Fact]
    public async Task Converge_MemorySizesMismatched_UpdateSizesWhichAreNotExplicitlyConfigured()
    {
        _fixture.Config.Capabilities =
        [
            new CatletCapabilityConfig
            {
                Name = EryphConstants.Capabilities.DynamicMemory,
            },
        ];
        _fixture.Config.Memory = new CatletMemoryConfig
        {
            Startup = 2048,
        };
        var vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo
        {
            DynamicMemoryEnabled = true,
            MemoryStartup = 512 * 1024L * 1024,
            MemoryMinimum = 4096 * 1024L * 1024,
            MemoryMaximum = 1024 * 1024L * 1024,
        });

        var result = await _convergeTask.Converge(vmInfo);
        result.Should().BeRight();

        _executedCommand.Should().NotBeNull();
        _executedCommand!.ShouldBeCommand("Set-VMMemory")
            .ShouldBeParam("VM", vmInfo.PsObject)
            .ShouldBeParam("StartupBytes", 2048 * 1024L * 1024)
            .ShouldBeParam("MinimumBytes", 2048 * 1024L * 1024)
            .ShouldBeParam("MaximumBytes", 2048 * 1024L * 1024)
            .ShouldBeComplete();
    }

    [Fact]
    public async Task Converge_NoChangesWithDynamicMemory_CommandIsNotExecuted()
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

        var result = await _convergeTask.Converge(vmInfo);
        result.Should().BeRight();

        _executedCommand.Should().BeNull();
    }

    [Fact]
    public async Task Converge_NoChangesWithoutDynamicMemory_CommandIsNotExecuted()
    {
        _fixture.Config.Memory = new CatletMemoryConfig
        {
            Startup = 2048,
        };
        var vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo
        {
            DynamicMemoryEnabled = false,
            MemoryStartup = 2048 * 1024L * 1024,
            MemoryMinimum = 512 * 1024L * 1024,
            MemoryMaximum = 1 * 1024L * 1024 *1024 * 1024,
        });

        var result = await _convergeTask.Converge(vmInfo);
        result.Should().BeRight();

        _executedCommand.Should().BeNull();
    }

    [Fact]
    public async Task Converge_InvalidMinimum_ReturnError()
    {
        _fixture.Config.Memory = new CatletMemoryConfig
        {
            Startup = 1024,
            Minimum = 2048,
        };
        var vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo
        {
            DynamicMemoryEnabled = true,
            MemoryStartup = 42 * 1024L * 1024,
            MemoryMinimum = 42 * 1024L * 1024,
            MemoryMaximum = 42 * 1024L * 1024,
        });

        var result = await _convergeTask.Converge(vmInfo);
        result.Should().BeLeft().Which.Message.Should().Be(
            "Startup memory (1024 MiB) cannot be less than minimum memory (2048 MiB).");

        _executedCommand.Should().BeNull();
    }

    [Fact]
    public async Task Converge_InvalidMaximum_ReturnError()
    {
        _fixture.Config.Memory = new CatletMemoryConfig
        {
            Startup = 4096,
            Maximum = 2048,
        };
        var vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo
        {
            DynamicMemoryEnabled = true,
            MemoryStartup = 42 * 1024L * 1024,
            MemoryMinimum = 42 * 1024L * 1024,
            MemoryMaximum = 42 * 1024L * 1024,
        });

        var result = await _convergeTask.Converge(vmInfo);
        result.Should().BeLeft().Which.Message.Should().Be(
            "Startup memory (4096 MiB) cannot be more than maximum memory (2048 MiB).");

        _executedCommand.Should().BeNull();
    }
}
