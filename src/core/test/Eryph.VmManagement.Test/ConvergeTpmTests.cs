using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.VmManagement.Converging;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Test;

public class ConvergeTpmTests
{
    private readonly ConvergeFixture _fixture = new();
    private readonly ConvergeTpm _convergeTask;
    private readonly TypedPsObject<VirtualMachineInfo> _vmInfo;

    public ConvergeTpmTests()
    {
        _convergeTask = new(_fixture.Context);
        _vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo());
    }

    [Fact]
    public async Task Converge_TpmShouldBeEnabled_CreatesProtectorAndEnablesTpm()
    {
        bool guardianCreated = false;
        bool protectorCreated = false;
        bool tpmEnabled = false;
        var cimHgsGuardian = _fixture.Engine.ToPsObject((object)new CimHgsGuardian());
        var protector = new byte[] { 1, 2, 3, 4, 5 };
        var cimHgsKeyProtector = _fixture.Engine.ToPsObject((object)new CimHgsKeyProtector()
        {
            RawData = protector,
        });

        _fixture.Config.Capabilities =
        [
            new CatletCapabilityConfig()
            {
                Name = EryphConstants.Capabilities.Tpm,
            }
        ];

        _fixture.Engine.GetObjectCallback = (_, command) =>
        {
            if (command.ToString().StartsWith("Get-HgsGuardian"))
            {
                command.ShouldBeCommand("Get-HgsGuardian")
                    .ShouldBeComplete();

                return Seq<TypedPsObject<object>>();
            }

            if (command.ToString().StartsWith("New-HgsKeyProtector"))
            {
                command.ShouldBeCommand("New-HgsKeyProtector")
                    .ShouldBeParam("Owner", cimHgsGuardian.PsObject)
                    .ShouldBeFlag("AllowUntrustedRoot")
                    .ShouldBeComplete();

                return Seq1(cimHgsKeyProtector);
            }

            if (command.ToString().StartsWith("Get-HgsGuardian"))
            {
                command.ShouldBeCommand("Get-HgsGuardian")
                    .ShouldBeComplete();

                return Seq<TypedPsObject<object>>();
            }

            if (command.ToString().StartsWith("New-HgsGuardian"))
            {
                command.ShouldBeCommand("New-HgsGuardian")
                    .ShouldBeParam("Name", EryphConstants.HgsGuardianName)
                    .ShouldBeFlag("GenerateCertificates")
                    .ShouldBeComplete();

                guardianCreated = true;
                return Seq1(cimHgsGuardian); 
            }
            
            return new PowershellFailure { Message = $"Unexpected command {command}" };
        };

        _fixture.Engine.GetValuesCallback = (_, command) =>
        {
            if (command.ToString().StartsWith("Get-VMSecurity"))
            {
                command.ShouldBeCommand("Get-VMSecurity")
                    .ShouldBeParam("VM", _vmInfo.PsObject)
                    .ShouldBeComplete();

                return Seq1<object>(new VMSecurityInfo()
                {
                    TpmEnabled = false,
                });
            }

            if (command.ToString().StartsWith("Get-VMKeyProtector"))
            {
                command.ShouldBeCommand("Get-VMKeyProtector")
                    .ShouldBeParam("VM", _vmInfo.PsObject)
                    .ShouldBeComplete();

                // Hyper-V returns this value when no protector exists
                return Seq1<object>(new byte[] { 0, 0, 0, 4 });
            }

            return new PowershellFailure { Message = $"Unexpected command {command}" };
        };

        _fixture.Engine.RunCallback = command =>
        {
            if (command.ToString().StartsWith("Set-VMKeyProtector"))
            {
                command.ShouldBeCommand("Set-VMKeyProtector")
                    .ShouldBeParam("VM", _vmInfo.PsObject)
                    .ShouldBeParam("KeyProtector", protector)
                    .ShouldBeComplete();

                protectorCreated = true;
                return unit;
            }

            if (command.ToString().StartsWith("Enable-VMTPM"))
            {
                command.ShouldBeCommand("Enable-VMTPM")
                    .ShouldBeParam("VM", _vmInfo.PsObject)
                    .ShouldBeComplete();

                tpmEnabled = true;
                return unit;
            }

            return new PowershellFailure { Message = $"Unexpected command {command}" };
        };

        var result = await _convergeTask.Converge(_vmInfo);

        result.IfLeft(e => e.Throw());
        
        result.Should().BeRight();
        guardianCreated.Should().BeTrue();
        protectorCreated.Should().BeTrue();
        tpmEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Converge_TpmShouldBeDisabled_DisablesTpm()
    {
        bool tpmDisabled = false;

        // No capabilities configured -> TPM must be disabled
        _fixture.Config.Capabilities = null;

        _fixture.Engine.GetValuesCallback = (_, command) =>
        {
            if (command.ToString().StartsWith("Get-VMSecurity"))
            {
                command.ShouldBeCommand("Get-VMSecurity")
                    .ShouldBeParam("VM", _vmInfo.PsObject)
                    .ShouldBeComplete();

                return Seq1<object>(new VMSecurityInfo()
                {
                    TpmEnabled = true,
                });
            }

            return new PowershellFailure { Message = $"Unexpected command {command}" };
        };

        _fixture.Engine.RunCallback = command =>
        {
            command.ShouldBeCommand("Disable-VMTPM")
                .ShouldBeParam("VM", _vmInfo.PsObject)
                .ShouldBeComplete();

            tpmDisabled = true;
            return unit;
        };

        var result = await _convergeTask.Converge(_vmInfo);
        
        result.Should().BeRight();
        tpmDisabled.Should().BeTrue();
    }


    [Theory, CombinatorialData]
    public async Task Converge_TpmInRequiredState_DoesNotModifyTpm(bool tpmState)
    {
        _fixture.Config.Capabilities =
        [
            new CatletCapabilityConfig()
            {
                Name = EryphConstants.Capabilities.Tpm,
                Details = tpmState ? [] : ["disabled"],
            }
        ];

        _fixture.Engine.GetValuesCallback = (_, command) =>
        {
            if (command.ToString().StartsWith("Get-VMSecurity"))
            {
                command.ShouldBeCommand("Get-VMSecurity")
                    .ShouldBeParam("VM", _vmInfo.PsObject)
                    .ShouldBeComplete();

                return Seq1<object>(new VMSecurityInfo()
                {
                    TpmEnabled = tpmState,
                });
            }

            return new PowershellFailure { Message = $"Unexpected command {command}" };
        };

        var result = await _convergeTask.Converge(_vmInfo);
        
        result.Should().BeRight();
    }
}
