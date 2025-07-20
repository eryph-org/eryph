using Eryph.Modules.VmHostAgent.Inventory;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.TestBase;
using Eryph.VmManagement.Wmi;
using Microsoft.Extensions.Logging.Abstractions;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Test.Inventory;

public class VmStateChangeWatcherServiceTests
{
    [Fact]
    public async Task OnEventArrived_ReturnsMessages()
    {
        var wmiObject = new WmiObject(HashMap(
            ("__CLASS", Optional<object>("Msvm_ComputerSystem")),
            ("Name", Optional<object>("2FE70974-C81A-4F3A-BF4E-7BE405B88C97")),
            ("EnabledState", Optional<object>(MsvmConstants.EnabledState.Enabled)),
            ("OtherEnabledState", Optional<object>(null)),
            ("HealthState", Optional<object>(MsvmConstants.HealthState.Ok)),
            ("OnTimeInMilliseconds", Optional<object>(42000uL))
            ));

        var timestamp = DateTimeOffset.UtcNow;
        var wmiEvent = new WmiEvent(timestamp, wmiObject);

        var result = await VmStateChangeWatcherService.OnEventArrived(wmiEvent, NullLogger.Instance).Run();

        var message = result.Should().BeSuccess().Which.Should().BeSome().Subject;
        message.VmId.Should().Be(Guid.Parse("2FE70974-C81A-4F3A-BF4E-7BE405B88C97"));
        message.State.Should().Be(VirtualMachineState.Running);
        message.UpTime.Should().Be(TimeSpan.FromSeconds(42));
        message.Timestamp.Should().Be(timestamp);
    }
}
