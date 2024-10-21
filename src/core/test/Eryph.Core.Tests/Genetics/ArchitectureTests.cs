using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Genetics;

namespace Eryph.Core.Tests.Genetics;

public class ArchitectureTests
{
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
    public void NewValidation_ValidData_ReturnsResult(
        string hypervisor,
        string expected)
    {
        var result = Architecture.NewValidation(hypervisor);

        result.Should().BeSuccess().Which.Value.Should().Be(expected);
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

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            error => error.Message.Should().Be(expected));
    }
}
