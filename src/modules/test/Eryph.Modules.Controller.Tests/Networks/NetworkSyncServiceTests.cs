using Dbosoft.OVN;
using Eryph.Modules.Controller.Networks;
using FluentAssertions;
using LanguageExt;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Tests.Networks;

public class NetworkSyncServiceTests
{
    [Fact]
    public void BuildClusterPlan_SingleChassis_PlanContainsGroupAndOneMember()
    {
        var chassis = Seq1((ChassisName: "local", Priority: (short)1));

        var plan = NetworkSyncService.BuildClusterPlan("local", chassis);

        plan.PlannedChassisGroups.Find("local").IsSome.Should().BeTrue();
        plan.PlannedChassisGroups["local"].Name.Should().Be("local");
        plan.PlannedChassis.Find("local").IsSome.Should().BeTrue();
        var planned = plan.PlannedChassis["local"];
        planned.ChassisGroupName.Should().Be("local");
        planned.Name.Should().Be("local");
        planned.Priority.Should().Be(1);
    }

    [Fact]
    public void BuildClusterPlan_NoChassis_PlanContainsGroupOnly()
    {
        var plan = NetworkSyncService.BuildClusterPlan(
            "local",
            Seq<(string ChassisName, short Priority)>());

        plan.PlannedChassisGroups.Find("local").IsSome.Should().BeTrue();
        plan.PlannedChassis.Count.Should().Be(0);
    }

    [Fact]
    public void BuildClusterPlan_MultipleChassis_AllMembersAdded()
    {
        var chassis = Seq(
            (ChassisName: "host-1", Priority: (short)20),
            (ChassisName: "host-2", Priority: (short)10));

        var plan = NetworkSyncService.BuildClusterPlan("ha-group", chassis);

        plan.PlannedChassisGroups.Find("ha-group").IsSome.Should().BeTrue();
        plan.PlannedChassis.Count.Should().Be(2);
        plan.PlannedChassis["host-1"].Priority.Should().Be(20);
        plan.PlannedChassis["host-2"].Priority.Should().Be(10);
        plan.PlannedChassis["host-1"].ChassisGroupName.Should().Be("ha-group");
        plan.PlannedChassis["host-2"].ChassisGroupName.Should().Be("ha-group");
    }
}
