using Eryph.Modules.VmHostAgent.Inventory;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Sys;
using Eryph.VmManagement.TestBase;
using Eryph.VmManagement.Wmi;
using FluentAssertions.Extensions;
using LanguageExt;
using Moq;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Test.Inventory;

public class WmiVmUptimeCheckJobTests
{
    private readonly Mock<WmiIO> _wmiIOMock = new();

    [Fact]
    public void Execute_WithResult_ReturnsMessage()
    {
        var wmiObject = new WmiObject(HashMap(
            ("__CLASS", Optional<object>("Msvm_ComputerSystem")),
            ("Name", Optional<object>("2FE70974-C81A-4F3A-BF4E-7BE405B88C97")),
            ("EnabledState", Optional<object>(MsvmConstants.EnabledState.Enabled)),
            ("OtherEnabledState", Optional<object>(null)),
            ("HealthState", Optional<object>(MsvmConstants.HealthState.Ok)),
            ("OnTimeInMilliseconds", Optional<object>(42000uL))
        ));

        ArrangeResults(Seq1(wmiObject));

        var timestamp = DateTimeOffset.UtcNow;
        var result = WmiVmUptimeCheckJob<TestRuntime>.Execute()
            .Run(TestRuntime.New(_wmiIOMock.Object));

        result.Should().BeSuccess().Which.Should().SatisfyRespectively(
            message =>
            {
                message.VmId.Should().Be(Guid.Parse("2FE70974-C81A-4F3A-BF4E-7BE405B88C97"));
                message.Status.Should().Be(VmStatus.Running);
                message.UpTime.Should().Be(TimeSpan.FromSeconds(42));
                message.Timestamp.Should().BeWithin(1.Seconds()).After(timestamp);
            });
    }

    [Fact]
    public void Execute_WithoutResult_ReturnsNothing()
    {
        ArrangeResults(Seq<WmiObject>());

        var result = WmiVmUptimeCheckJob<TestRuntime>.Execute()
            .Run(TestRuntime.New(_wmiIOMock.Object));

        result.Should().BeSuccess()
            .Which.Should().BeEmpty();
    }

    private void ArrangeResults(Seq<WmiObject> wmiObjects)
    {
        _wmiIOMock.Setup(m => m.ExecuteQuery(
                @"root\virtualization\v2",
                Seq("__CLASS", "Name", "EnabledState", "OtherEnabledState", "HealthState", "OnTimeInMilliseconds"),
                "Msvm_ComputerSystem",
                "OnTimeInMilliseconds <> NULL AND OnTimeInMilliseconds < 3600000"))
            .Returns(FinSucc(wmiObjects));
    }

    private readonly struct TestRuntime(WmiIO wmiIO) : HasWmi<TestRuntime>
    {
        private readonly WmiIO _wmiIO = wmiIO;

        public static TestRuntime New(WmiIO wmiIO) => new(wmiIO);

        public Eff<TestRuntime, WmiIO> WmiEff => Eff<TestRuntime, WmiIO>(rt => rt._wmiIO);
    }
}
