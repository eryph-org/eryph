using System.Linq;
using Eryph.Core.Genetics;

namespace Eryph.Core.Tests.Genetics;

public class ArchitectureTests
{
    [Theory]
    [InlineData("any", "any")]
    [InlineData("hyperv/amd64", "hyperv/amd64, hyperv/any, any")]
    [InlineData("kvm/amd64", "kvm/amd64, kvm/any, any")]
    [InlineData("hyperv/any", "hyperv/any, any")]
    // A derived hypervisor falls back to its base after its own architectures.
    [InlineData("azure/amd64", "azure/amd64, azure/any, hyperv/amd64, hyperv/any, any")]
    [InlineData("ec2/amd64", "ec2/amd64, ec2/any, kvm/amd64, kvm/any, any")]
    [InlineData("azure/any", "azure/any, hyperv/any, any")]
    public void GeneResolutionOrder_ReturnsCandidatesInOrder(
        string architecture,
        string expectedOrder)
    {
        var order = Architecture.New(architecture).GeneResolutionOrder.Map(a => a.Value);

        string.Join(", ", order).Should().Be(expectedOrder);
    }

    [Theory]
    [InlineData("any", true)]
    [InlineData("hyperv/any", false)]
    [InlineData("hyperv/amd64", false)]
    public void IsAny_ReturnsCorrectResult(
        string hypervisor,
        bool expected)
    {
        Architecture.New(hypervisor).IsAny.Should().Be(expected);
    }

    [Theory]
    [InlineData("any", "any")]
    [InlineData("ANY", "any")]
    [InlineData("any/any", "any")]
    [InlineData("ANY/ANY", "any")]
    [InlineData("hyperv/any", "hyperv/any")]
    [InlineData("HYPERV/ANY", "hyperv/any")]
    [InlineData("hyperv/amd64", "hyperv/amd64")]
    [InlineData("HYPERV/AMD64", "hyperv/amd64")]
    [InlineData("azure/amd64", "azure/amd64")]
    [InlineData("AZURE/AMD64", "azure/amd64")]
    [InlineData("azure/any", "azure/any")]
    public void NewValidation_ValidData_ReturnsResult(
        string hypervisor,
        string expected)
    {
        var result = Architecture.NewValidation(hypervisor);

        result.Should().BeSuccess().Which.Value.Should().Be(expected);
    }

    [Theory]
    // Concrete host hyperv/amd64: exact and wildcard requests are satisfied.
    [InlineData("hyperv/amd64", "hyperv/amd64", true)]
    [InlineData("hyperv/any", "hyperv/amd64", true)]
    [InlineData("any", "hyperv/amd64", true)]
    // Mismatched hypervisor or processor cannot be placed on the host.
    [InlineData("azure/amd64", "hyperv/amd64", false)]
    [InlineData("kvm/amd64", "hyperv/amd64", false)]
    [InlineData("azure/any", "hyperv/amd64", false)]
    public void IsSatisfiedBy_ReturnsCorrectResult(
        string architecture,
        string hostArchitecture,
        bool expected)
    {
        Architecture.New(architecture)
            .IsSatisfiedBy(Architecture.New(hostArchitecture))
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(null!, "The value cannot be null.")]
    [InlineData("", "The architecture cannot be empty.")]
    [InlineData("a", "The architecture is invalid.")]
    [InlineData("a/b/c", "The architecture is invalid.")]
    [InlineData("qemu/amd64", "The hypervisor is invalid.")]
    [InlineData("hyperv/x86", "The processor architecture is invalid.")]
    public void NewValidation_InvalidData_ReturnsError(
        string architecture,
        string expected)
    {
        var result = Architecture.NewValidation(architecture);

        result.Should().BeFail().Which.Should().SatisfyRespectively(error => error.Message.Should().Be(expected));
    }
}
