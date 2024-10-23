using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Genetics;
using LanguageExt.Common;

namespace Eryph.Core.Tests.Genetics;

public class ProcessorArchitectureTests
{
    [Theory]
    [InlineData("any", true)]
    [InlineData("amd64", false)]
    public void IsAny_ReturnsCorrectResult(
        string architecture,
        bool expected)
    {
        ProcessorArchitecture.New(architecture).IsAny.Should().Be(expected);
    }

    [Theory]
    [InlineData("any", "any")]
    [InlineData("ANY", "any")]
    [InlineData("amd64", "amd64")]
    [InlineData("AMD64", "amd64")]
    public void NewValidation_ValidData_ReturnsResult(
        string architecture,
        string expected)
    {
        var result = ProcessorArchitecture.NewValidation(architecture);

        result.Should().BeSuccess().Which.Value.Should().Be(expected);
    }

    [Fact]
    public void NewValidation_InvalidData_ReturnsError()
    {
        var result = ProcessorArchitecture.NewValidation("x86");

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            error => error.Message.Should().Be("The processor architecture is invalid."));
    }
}
