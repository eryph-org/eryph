using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Dbosoft.Functional.Validations;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Modules.AspNetCore.Tests;

public class ValidationIssueExtensionsTests
{
    [Fact]
    public void ToModelStateDictionary_EmptyPrefix_ReturnsCorrectPaths()
    {
        var validation = Fail<ValidationIssue, Unit>(
            new ValidationIssue("$.test_property", "error message"));

        var modelState = validation.ToModelStateDictionary();
        var modelStateEntry = modelState.Should().ContainKey("$.test_property").WhoseValue;
        modelStateEntry.Should().NotBeNull();
        modelStateEntry!.Errors.Should().SatisfyRespectively(
            modelError => modelError.ErrorMessage.Should().Be("error message"));
    }

    [Fact]
    public void ToModelStateDictionary_WithPrefix_ReturnsCorrectPaths()
    {
        var validation = Fail<ValidationIssue, Unit>(
            new ValidationIssue("$.test_property", "error message"));

        var modelState = validation.ToModelStateDictionary("RequestProperty");
        var modelStateEntry = modelState.Should().ContainKey("$.request_property.test_property").WhoseValue;
        modelStateEntry.Should().NotBeNull();
        modelStateEntry!.Errors.Should().SatisfyRespectively(
            modelError => modelError.ErrorMessage.Should().Be("error message"));
    }
}
