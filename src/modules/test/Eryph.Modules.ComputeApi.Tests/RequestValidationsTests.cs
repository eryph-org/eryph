using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using Xunit;

namespace Eryph.Modules.ComputeApi.Tests;

public class RequestValidationsTests
{
    [Fact]
    public void ValidateCatletConfig_InvalidValue_ReturnsIssueWithJsonPath()
    {
        using var document = JsonDocument.Parse("""
                                                {
                                                  "network_adapters":
                                                  [
                                                    {
                                                      "name": 42
                                                    }
                                                  ]
                                                }
                                                """);

        var result = RequestValidations.ValidateCatletConfig(document.RootElement);
        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("$.network_adapters[0].name");
                issue.Message.Should().StartWith("The JSON value could not be converted to System.String");
            });
    }

    [Fact]
    public void ValidateCatletConfig_InvalidName_ReturnsIssueWithJsonPath()
    {
        using var document = JsonDocument.Parse("""
                                                {
                                                  "network_adapters":
                                                  [
                                                    {
                                                      "name": "eth$"
                                                    }
                                                  ]
                                                }
                                                """);


        var result = RequestValidations.ValidateCatletConfig(document.RootElement);
        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("$.network_adapters[0].name");
                issue.Message.Should().StartWith("The catlet network adapter name contains invalid characters. Only latin characters, numbers and hyphens are permitted.");
            });
    }

    [Fact]
    public void ValidateProjectNetworkConfig_InvalidValue_ReturnsIssueWithJsonPath()
    {
        using var document = JsonDocument.Parse("""
                                                {
                                                  "networks":
                                                  [
                                                    {
                                                      "provider":
                                                      {
                                                        "ip_pool": 42
                                                      }
                                                    }
                                                  ]
                                                }
                                                """);

        var result = RequestValidations.ValidateProjectNetworkConfig(document.RootElement);
        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("$.networks[0].provider.ip_pool");
                issue.Message.Should().StartWith("The JSON value could not be converted to System.String");
            });
    }

    [Fact]
    public void ValidateProjectNetworkConfig_InvalidName_ReturnsIssueWithJsonPath()
    {
        using var document = JsonDocument.Parse("""
                                                {
                                                  "project": "project$"
                                                }
                                                """);


        var result = RequestValidations.ValidateCatletConfig(document.RootElement);
        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("$.project");
                issue.Message.Should().StartWith("The project name contains invalid characters. Only latin characters, numbers, dots and hyphens are permitted.");
            });
    }
}
