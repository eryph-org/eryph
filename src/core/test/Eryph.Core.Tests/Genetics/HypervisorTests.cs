using Eryph.Core.Genetics;

namespace Eryph.Core.Tests.Genetics;

public class HypervisorTests
{
    [Theory]
    [InlineData("any", true)]
    [InlineData("hyperv", false)]
    public void IsAny_ReturnsCorrectResult(
        string hypervisor,
        bool expected)
    {
        Hypervisor.New(hypervisor).IsAny.Should().Be(expected);
    }

    [Theory]
    [InlineData("any", "any")]
    [InlineData("ANY", "any")]
    [InlineData("hyperv", "hyperv")]
    [InlineData("HYPERV", "hyperv")]
    [InlineData("azure", "azure")]
    [InlineData("AZURE", "azure")]
    [InlineData("kvm", "kvm")]
    [InlineData("ec2", "ec2")]
    public void NewValidation_ValidData_ReturnsResult(
        string hypervisor,
        string expected)
    {
        var result = Hypervisor.NewValidation(hypervisor);

        result.Should().BeSuccess().Which.Value.Should().Be(expected);
    }

    [Fact]
    public void NewValidation_InvalidData_ReturnsError()
    {
        var result = Hypervisor.NewValidation("qemu");

        result.Should().BeFail().Which.Should()
            .SatisfyRespectively(error => error.Message.Should().Be("The hypervisor is invalid."));
    }

    [Theory]
    [InlineData("azure", "hyperv")]
    [InlineData("ec2", "kvm")]
    public void BaseHypervisors_DerivedHypervisor_ReturnsBase(
        string hypervisor, string expectedBase)
    {
        Hypervisor.New(hypervisor).BaseHypervisors
            .Should().SatisfyRespectively(b => b.Value.Should().Be(expectedBase));
    }

    [Theory]
    [InlineData("hyperv")]
    [InlineData("kvm")]
    [InlineData("any")]
    public void BaseHypervisors_BaseHypervisor_ReturnsEmpty(string hypervisor)
    {
        Hypervisor.New(hypervisor).BaseHypervisors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("hyperv", "hyperv", true)] // same hypervisor
    [InlineData("hyperv", "any", true)] // wildcard gene
    [InlineData("azure", "hyperv", true)] // gene for the base hypervisor
    [InlineData("ec2", "kvm", true)] // gene for the base hypervisor
    [InlineData("azure", "azure", true)]
    [InlineData("hyperv", "azure", false)] // derived gene does not satisfy the base
    [InlineData("kvm", "ec2", false)] // derived gene does not satisfy the base
    [InlineData("azure", "kvm", false)] // unrelated hypervisor
    [InlineData("ec2", "hyperv", false)] // unrelated lineage (ec2 derives from kvm)
    [InlineData("kvm", "azure", false)] // unrelated lineage
    [InlineData("any", "hyperv", false)] // a wildcard catlet is not satisfied by a concrete gene
    public void AcceptsGenesFrom_ReturnsCorrectResult(
        string catletHypervisor, string geneHypervisor, bool expected)
    {
        Hypervisor.New(catletHypervisor)
            .AcceptsGenesFrom(Hypervisor.New(geneHypervisor))
            .Should().Be(expected);
    }
}
