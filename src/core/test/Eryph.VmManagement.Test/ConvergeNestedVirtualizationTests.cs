using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.VmManagement.Converging;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using FluentAssertions;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Test;

public class ConvergeNestedVirtualizationTests
{
    private readonly ConvergeFixture _fixture = new();
    private readonly ConvergeNestedVirtualization _convergeTask;
    private readonly TypedPsObject<VirtualMachineInfo> _vmInfo;
    private AssertCommand? _executedCommand;

    public ConvergeNestedVirtualizationTests()
    {
        _convergeTask = new(_fixture.Context);
        _vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo()
        {
            State = VirtualMachineState.Off,
        });
        _fixture.Engine.RunCallback = cmd =>
        {
            _executedCommand = cmd;
            return unit;
        };
    }

    [Theory, CombinatorialData]
    public async Task Converge_EnablesNestedVirtualizationWhenNecessary(bool exposed, bool? shouldExpose)
    {
        _fixture.Config.Capabilities = shouldExpose.HasValue switch
        {
            true =>
            [
                new CatletCapabilityConfig
                {
                    Name = EryphConstants.Capabilities.NestedVirtualization,
                    Details = shouldExpose.GetValueOrDefault()
                        ? [EryphConstants.CapabilityDetails.Enabled]
                        : [EryphConstants.CapabilityDetails.Disabled],
                }
            ],
            false => null,
        };

        _fixture.Engine.GetValuesCallback = (_, command) =>
        {
            command.ShouldBeCommand("Get-VMProcessor")
                .ShouldBeParam("VM", _vmInfo.PsObject)
                .ShouldBeComplete();

            return Seq1<object>(new VMProcessorInfo
            {
                ExposeVirtualizationExtensions = exposed,
            });
        };

        await _convergeTask.Converge(_vmInfo);

        if (exposed == shouldExpose.GetValueOrDefault())
        {
            _executedCommand.Should().BeNull();
            return;
        }
        
        _executedCommand.Should().NotBeNull();
        _executedCommand!.ShouldBeCommand("Set-VMProcessor")
            .ShouldBeParam("VM")
            .ShouldBeParam("ExposeVirtualizationExtensions", shouldExpose.GetValueOrDefault());
    }
}
