using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.VmManagement.Converging;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using FluentAssertions;
using LanguageExt;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Test;

public class ConvergeSecureBootTests
{
    private readonly ConvergeFixture _fixture = new();
    private readonly ConvergeSecureBoot _convergeTask;
    private readonly TypedPsObject<VirtualMachineInfo> _vmInfo;
    private AssertCommand? _executedCommand;

    public ConvergeSecureBootTests()
    {
        _convergeTask = new(_fixture.Context);
        _vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo());
        _fixture.Engine.RunCallback = cmd =>
        {
            _executedCommand = cmd;
            return unit;
        };
    }

    [Theory, CombinatorialData]
    public async Task Converge_EnablesSecureBootWhenNecessary(
        bool secureBoot,
        bool? shouldSecureBoot)
    {
        _fixture.Config.Capabilities = shouldSecureBoot.HasValue switch
        {
            true =>
            [
                new CatletCapabilityConfig
                {
                    Name = EryphConstants.Capabilities.NestedVirtualization,
                    Details = shouldSecureBoot.Value
                        ? [EryphConstants.CapabilityDetails.Enabled]
                        : [EryphConstants.CapabilityDetails.Disabled],
                }
            ],
            false => null,
        };

        _fixture.Engine.GetValuesCallback = (_, command) =>
        {
            command.ShouldBeCommand("Get-VMFirmware")
                .ShouldBeParam("VM", _vmInfo.PsObject)
                .ShouldBeComplete();

            return Seq1<object>(new VMFirmwareInfo
            {
                SecureBoot = secureBoot ? OnOffState.On : OnOffState.Off,
                SecureBootTemplate = "MicrosoftWindows",
            });
        };

        await _convergeTask.Converge(_vmInfo);

        if (secureBoot == shouldSecureBoot.GetValueOrDefault())
        {
            _executedCommand.Should().BeNull();
            return;
        }

        _executedCommand.Should().NotBeNull();
        _executedCommand!.ShouldBeCommand("Set-VMFirmware")
            .ShouldBeParam("VM")
            .ShouldBeParam(
                "EnableSecureBoot",
                shouldSecureBoot.GetValueOrDefault() ? OnOffState.On : OnOffState.Off);
    }
}
