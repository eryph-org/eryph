using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.TestBase;
using Eryph.VmManagement.Wmi;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using Xunit;

using static Eryph.VmManagement.Wmi.WmiMsvmUtils;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Test.Wmi;

public class WmiMsvmUtilsTests
{
    [Fact]
    public void GetVmId_ComputerSystemWithVmId_ReturnsId()
    {
        var wmiObject = new WmiObject(HashMap(
            ("__CLASS", Optional<object>("Msvm_ComputerSystem")),
            ("Name", Optional<object>("2FE70974-C81A-4F3A-BF4E-7BE405B88C97"))));

        var result = getVmId(wmiObject).Run();

        result.Should().BeSuccess()
            .Which.Should().BeSome()
            .Which.Should().Be(Guid.Parse("2FE70974-C81A-4F3A-BF4E-7BE405B88C97"));
    }

    [Fact]
    public void GetVmId_ComputerSystemWithoutVmId_ReturnsId()
    {
        var wmiObject = new WmiObject(HashMap(
            ("__CLASS", Optional<object>("Msvm_ComputerSystem")),
            ("Name", Optional<object>("TestHost"))));

        var result = getVmId(wmiObject).Run();

        result.Should().BeSuccess()
            .Which.Should().BeNone();
    }

    [Fact]
    public void GetVmId_GuestNetworkAdapterConfigurationWithValidId_ReturnsId()
    {
        var wmiObject = new WmiObject(HashMap(
            ("__CLASS", Optional<object>("Msvm_GuestNetworkAdapterConfiguration")),
            ("InstanceID", Optional<object>(@"Microsoft:GuestNetwork\2FE70974-C81A-4F3A-BF4E-7BE405B88C97\596574F5-A810-43EF-B349-D20783874CE5"))));

        var result = getVmId(wmiObject).Run();

        result.Should().BeSuccess()
            .Which.Should().BeSome()
            .Which.Should().Be(Guid.Parse("2FE70974-C81A-4F3A-BF4E-7BE405B88C97"));
    }

    [Fact]
    public void GetVmId_GuestNetworkAdapterConfigurationWithInvalidId_ReturnsError()
    {
        var wmiObject = new WmiObject(HashMap(
            ("__CLASS", Optional<object>("Msvm_GuestNetworkAdapterConfiguration")),
            ("InstanceID", Optional<object>("InvalidId"))));

        var result = getVmId(wmiObject).Run();

        result.Should().BeFail()
            .Which.Message.Should().Be("The instance ID 'InvalidId' is malformed.");
    }

    [Fact]
    public void GetVmId_InvalidClass_ReturnsError()
    {
        var wmiObject = new WmiObject(HashMap(
            ("__CLASS", Optional<object>("InvalidClass"))));

        var result = getVmId(wmiObject).Run();

        result.Should().BeFail()
            .Which.Message.Should().Be("WMI objects of type 'InvalidClass' are not supported.");
    }

    [Fact]
    public void GetVmState_ValidState_ReturnsState()
    {
        var wmiObject = new WmiObject(HashMap(
            ("EnabledState", Optional<object>(MsvmConstants.EnabledState.Enabled)),
            ("OtherEnabledState", Optional<object>(null)),
            ("HealthState", Optional<object>(MsvmConstants.HealthState.CriticalFailure))));

        var result = getVmState(wmiObject).Run();

        result.Should().BeSuccess()
            .Which.Should().Be(VirtualMachineState.RunningCritical);
    }

    [Fact]
    public void GetOperationalStatus_ValidStatus_ReturnsStatus()
    {
        var wmiObject = new WmiObject(HashMap(
            ("OperationalStatus", Optional<object>(new[] { MsvmConstants.OperationalStatus.Ok }))));

        var result = getOperationalStatus(wmiObject).Run();

        result.Should().BeSuccess()
            .Which.Should().Be(VirtualMachineOperationalStatus.Ok);
    }

    [Fact]
    public void GetVmUpTime_ValidUpTime_ReturnsUpTime()
    {
        var wmiObject = new WmiObject(HashMap(
            ("OnTimeInMilliseconds", Optional<object>(175646uL))));

        var result = getVmUpTime(wmiObject).Run();

        result.Should().BeSuccess()
            .Which.Should().Be(TimeSpan.FromMilliseconds(175646));
    }

    [Fact]
    public void GetVmUpTime_UpTimeIsNull_ReturnsZero()
    {
        var wmiObject = new WmiObject(HashMap(
            ("OnTimeInMilliseconds", Optional<object>(null))));

        var result = getVmUpTime(wmiObject).Run();

        result.Should().BeSuccess()
            .Which.Should().Be(TimeSpan.Zero);
    }
}
