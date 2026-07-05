using Eryph.ModuleCore;
using FluentAssertions;
using Xunit;

namespace Eryph.ModuleCore.Tests;

public class DistributedEndpointResolverTests
{
    [Fact]
    public void TryGetRawEndpoint_Present_ReturnsTrueAndRawValue()
    {
        var resolver = new DistributedEndpointResolver();
        resolver.Update(new Dictionary<string, string> { ["ovn-southbound"] = "ssl:host:6642" });

        resolver.TryGetRawEndpoint("ovn-southbound", out var value).Should().BeTrue();
        value.Should().Be("ssl:host:6642");
    }

    [Fact]
    public void TryGetRawEndpoint_Missing_ReturnsFalse()
    {
        var resolver = new DistributedEndpointResolver();

        resolver.TryGetRawEndpoint("ovn-southbound", out var value).Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void TryGetRawEndpoint_Whitespace_ReturnsFalse()
    {
        var resolver = new DistributedEndpointResolver();
        resolver.Update(new Dictionary<string, string> { ["blank"] = "   " });

        resolver.TryGetRawEndpoint("blank", out _).Should().BeFalse();
    }

    [Fact]
    public void Update_ChangedContent_RaisesChanged()
    {
        var resolver = new DistributedEndpointResolver();
        resolver.Update(new Dictionary<string, string> { ["a"] = "1" });

        var raised = 0;
        resolver.Changed += (_, _) => raised++;
        resolver.Update(new Dictionary<string, string> { ["a"] = "2" });

        raised.Should().Be(1);
    }

    [Fact]
    public void Update_IdenticalContent_DoesNotRaiseChanged()
    {
        var resolver = new DistributedEndpointResolver();
        resolver.Update(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });

        var raised = 0;
        resolver.Changed += (_, _) => raised++;
        // Same content, different insertion order — must be treated as no change.
        resolver.Update(new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" });

        raised.Should().Be(0);
    }
}
