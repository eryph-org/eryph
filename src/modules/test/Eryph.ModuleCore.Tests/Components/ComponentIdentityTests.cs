using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using FluentAssertions;

namespace Eryph.ModuleCore.Tests.Components;

/// <summary>
/// Verifies the component identity: a stable, deterministic <see cref="ComponentIdentity.ComponentId"/>
/// (derived from component type + host FQDN, independent of the run or the inbound queue) and a
/// per-run <see cref="ComponentIdentity.InstanceId"/>.
/// </summary>
public class ComponentIdentityTests
{
    [Fact]
    public void ComponentId_is_stable_across_instances_regardless_of_queue_or_run()
    {
        var a = new ComponentIdentity(ComponentType.Identity, "eryph.identity.host");
        var b = new ComponentIdentity(ComponentType.Identity, "a.different.queue");

        // Same component type on the same host => same stable id, even across restarts/queues.
        a.ComponentId.Should().Be(b.ComponentId);
        a.ComponentId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ComponentId_differs_by_component_type()
    {
        var identity = new ComponentIdentity(ComponentType.Identity, "q");
        var compute = new ComponentIdentity(ComponentType.ComputeApi, "q");
        var agent = new ComponentIdentity(ComponentType.VMHostAgent, "q");

        new[] { identity.ComponentId, compute.ComponentId, agent.ComponentId }
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void InstanceId_is_unique_per_instance()
    {
        var a = new ComponentIdentity(ComponentType.Identity, "q");
        var b = new ComponentIdentity(ComponentType.Identity, "q");

        a.InstanceId.Should().NotBe(b.InstanceId);
    }

    [Fact]
    public void MachineName_is_populated()
    {
        var identity = new ComponentIdentity(ComponentType.Identity, "q");

        // FQDN, or the short host name when not domain-joined — never empty.
        identity.MachineName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AdvertisedEndpoints_defaults_to_empty_and_carries_provided_values()
    {
        var none = new ComponentIdentity(ComponentType.VMHostAgent, "q");
        none.AdvertisedEndpoints.Should().BeEmpty();

        var advertised = new ComponentIdentity(
            ComponentType.Identity, "q",
            new Dictionary<string, string> { ["identity"] = "https://host/identity" });
        advertised.AdvertisedEndpoints.Should().ContainKey("identity")
            .WhoseValue.Should().Be("https://host/identity");
    }
}
