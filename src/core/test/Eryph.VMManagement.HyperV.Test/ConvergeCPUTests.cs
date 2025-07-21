using Eryph.ConfigModel.Catlets;
using Eryph.VmManagement.Converging;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Full;
using FluentAssertions;
using LanguageExt;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.HyperV.Test;

public class ConvergeCpuTests
{
    private readonly ConvergeFixture _fixture = new();
    private readonly ConvergeCPU _convergeTask;
    private AssertCommand? _executedCommand;

    public ConvergeCpuTests()
    {
        _convergeTask = new(_fixture.Context);
        _fixture.Engine.RunCallback = cmd =>
        {
            _executedCommand = cmd;
            return unit;
        };
    }

    [Theory]
    [InlineData(null, 1L)]
    [InlineData(null, 2L)]
    [InlineData(1, 1L)]
    [InlineData(2, 1L)]
    public async Task Converges_Cpu_if_necessary(int? configCpu, long vmCpu)
    {
        _fixture.Config.Cpu = new CatletCpuConfig { Count = configCpu };
        var vmData = _fixture.Engine.ToPsObject(new VirtualMachineInfo
        {
            State = VirtualMachineState.Off,
            ProcessorCount = vmCpu,
        });
        
        await _convergeTask.Converge(vmData);

        if (configCpu.GetValueOrDefault(1) == vmCpu)
        {
            _executedCommand.Should().BeNull();
            return;
        }

        _executedCommand.Should().NotBeNull();
        _executedCommand!.ShouldBeCommand("Set-VMProcessor")
            .ShouldBeParam("VM", vmData.PsObject)
            .ShouldBeParam("Count", configCpu.GetValueOrDefault(1));
    }
}
