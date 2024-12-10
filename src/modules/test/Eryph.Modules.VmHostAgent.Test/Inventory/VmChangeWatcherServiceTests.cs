using Eryph.Modules.VmHostAgent.Inventory;
using Eryph.VmManagement.TestBase;
using Eryph.VmManagement.Wmi;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Test.Inventory;

public class VmChangeWatcherServiceTests
{
    [Fact]
    public async Task OnEventArrived_ComputerSystemInValidState_ReturnsMessages()
    {
        var wmiObject = new WmiObject(HashMap(
            ("__CLASS", Optional<object>("Msvm_ComputerSystem")),
            ("Name", Optional<object>("2FE70974-C81A-4F3A-BF4E-7BE405B88C97")),
            ("EnabledState", Optional<object>(MsvmConstants.EnabledState.Enabled)),
            ("OtherEnabledState", Optional<object>(null)),
            ("HealthState", Optional<object>(MsvmConstants.HealthState.Ok)),
            ("OperationalStatus", Optional<object>(new[] { MsvmConstants.OperationalStatus.Ok }))
            ));

        var wmiEvent = new WmiEvent(DateTimeOffset.UtcNow, wmiObject);

        var result = await VmChangeWatcherService.OnEventArrived(wmiEvent).Run();

        result.ThrowIfFail();

        result.Should().BeSuccess()
            .Which.Should().BeSome()
            .Which.VmId.Should().Be(Guid.Parse("2FE70974-C81A-4F3A-BF4E-7BE405B88C97"));
    }

    [Fact]
    public async Task OnEventArrived_ComputerSystemInService_ReturnsNothing()
    {
        var wmiObject = new WmiObject(HashMap(
            ("__CLASS", Optional<object>("Msvm_ComputerSystem")),
            ("Name", Optional<object>("2FE70974-C81A-4F3A-BF4E-7BE405B88C97")),
            ("EnabledState", Optional<object>(MsvmConstants.EnabledState.Enabled)),
            ("OtherEnabledState", Optional<object>(null)),
            ("HealthState", Optional<object>(MsvmConstants.HealthState.Ok)),
            ("OperationalStatus", Optional<object>(new[] { MsvmConstants.OperationalStatus.InService }))
        ));

        var wmiEvent = new WmiEvent(DateTimeOffset.UtcNow, wmiObject);

        var result = await VmChangeWatcherService.OnEventArrived(wmiEvent).Run();

        result.Should().BeSuccess()
            .Which.Should().BeNone();
    }

    [Fact]
    public async Task OnEventArrived_GuestNetworkAdapterConfig_ReturnsMessages()
    {
        var wmiObject = new WmiObject(HashMap(
            ("__CLASS", Optional<object>("Msvm_GuestNetworkAdapterConfiguration")),
            ("InstanceID", Optional<object>(@"Microsoft:GuestNetwork\2FE70974-C81A-4F3A-BF4E-7BE405B88C97\596574F5-A810-43EF-B349-D20783874CE5"))
            ));

        var wmiEvent = new WmiEvent(DateTimeOffset.UtcNow, wmiObject);

        var result = await VmChangeWatcherService.OnEventArrived(wmiEvent).Run();

        result.Should().BeSuccess()
            .Which.Should().BeSome()
            .Which.VmId.Should().Be(Guid.Parse("2FE70974-C81A-4F3A-BF4E-7BE405B88C97"));
    }
}
