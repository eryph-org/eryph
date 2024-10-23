using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            error => error.Message.Should().Be("The hypervisor is invalid."));
    }
}
