using System.Text.Json;
using Eryph.Core.Network;
using Eryph.ModuleCore.Configuration;
using Eryph.Modules.Controller.Components;
using Eryph.Modules.Controller.Networks;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// Verifies the <see cref="ConfigDomain.OvnCluster"/> payload the controller distributes to the
/// network component: the gateway chassis topology from <see cref="IClusterTopologyProvider"/>,
/// serialized as <see cref="OvnClusterConfig"/>. The network component realizes it against its local
/// northbound database. (Replaces the previous NetworkSyncService.BuildClusterPlan coverage, which
/// moved here when chassis application moved into the network module.)
/// </summary>
public class OvnClusterConfigSourceTests
{
    private sealed class FakeTopology(
        string groupName,
        Seq<(string ChassisName, short Priority)> chassis)
        : IClusterTopologyProvider
    {
        public string ChassisGroupName => groupName;

        public Seq<(string ChassisName, short Priority)> GetChassis() => chassis;
    }

    private static async Task<OvnClusterConfig> BuildAsync(
        string group, Seq<(string ChassisName, short Priority)> chassis)
    {
        var source = new OvnClusterConfigSource(new FakeTopology(group, chassis));
        var json = await source.BuildPayloadAsync(ConfigScope.Default, CancellationToken.None);
        return JsonSerializer.Deserialize<OvnClusterConfig>(json)!;
    }

    [Fact]
    public async Task Payload_with_single_chassis_contains_group_and_one_member()
    {
        var config = await BuildAsync("local", Seq1(("local", (short)1)));

        config.ChassisGroupName.Should().Be("local");
        config.Chassis.Should().ContainSingle();
        config.Chassis[0].Name.Should().Be("local");
        config.Chassis[0].Priority.Should().Be(1);
    }

    [Fact]
    public async Task Payload_with_no_chassis_contains_group_only()
    {
        var config = await BuildAsync("local", Seq<(string ChassisName, short Priority)>());

        config.ChassisGroupName.Should().Be("local");
        config.Chassis.Should().BeEmpty();
    }

    [Fact]
    public async Task Payload_with_multiple_chassis_contains_all_members()
    {
        var config = await BuildAsync(
            "ha-group",
            Seq(("host-1", (short)20), ("host-2", (short)10)));

        config.ChassisGroupName.Should().Be("ha-group");
        config.Chassis.Should().HaveCount(2);
        config.Chassis.Should().Contain(c => c.Name == "host-1" && c.Priority == 20);
        config.Chassis.Should().Contain(c => c.Name == "host-2" && c.Priority == 10);
    }
}
