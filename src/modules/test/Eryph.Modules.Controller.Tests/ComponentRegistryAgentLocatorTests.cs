using Eryph.Core;
using LanguageExt;

namespace Eryph.Modules.Controller.Tests;

/// <summary>
/// Verifies that a datastore is resolved to an agent in the given site. Callers pass the site pinned
/// on an existing resource, or the site they resolved from an environment when creating one — the
/// locator itself never derives it.
/// </summary>
public class ComponentRegistryAgentLocatorTests
{
    private static readonly Guid BerlinSiteId = Guid.NewGuid();
    private static readonly Guid MunichSiteId = Guid.NewGuid();

    [Fact]
    public void FindAgentForDataStore_ReturnsTheFirstAgentInTheSite()
    {
        var locator = CreateLocator(
            Agent("munich-host", MunichSiteId),
            Agent("berlin-host", BerlinSiteId));

        var result = locator.FindAgentForDataStore("default", BerlinSiteId);

        result.IfLeft(e => throw new Exception(e.Message)).Should().Be("berlin-host");
    }

    [Fact]
    public void FindAgentForDataStore_NoAgentInTheSite_IsAnError()
    {
        var locator = CreateLocator(Agent("munich-host", MunichSiteId));

        var result = locator.FindAgentForDataStore("fast", BerlinSiteId);

        result.IsLeft.Should().BeTrue();
        result.MapLeft(e => e.Message.Should().Contain("'fast'"));
    }

    [Fact]
    public void FindAgentForGenePool_IsNotSiteBound()
    {
        var locator = CreateLocator(Agent("munich-host", MunichSiteId));

        locator.FindAgentForGenePool().Should().Be("munich-host");
    }

    private static HostAgentComponent Agent(string name, Guid siteId) =>
        new(name, siteId, EryphConstants.Networking.LocalChassisName, 1);

    private static ComponentRegistryAgentLocator CreateLocator(params HostAgentComponent[] agents) =>
        new(new StubRegistry(agents.ToSeq()));

    private sealed class StubRegistry(Seq<HostAgentComponent> agents) : IComponentRegistry
    {
        public Seq<HostAgentComponent> GetHostAgents() => agents;
    }
}
