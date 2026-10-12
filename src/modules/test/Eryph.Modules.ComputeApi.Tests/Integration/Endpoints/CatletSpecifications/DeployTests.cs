using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.TestBase;
using Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.CatletSpecifications;

public class DeployTests(ITestOutputHelper outputHelper)
    : CatletSpecificationTestBase(outputHelper)
{
    [Fact]
    public async Task Specification_is_not_deployed_when_version_belongs_to_other_specification()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .PostAsJsonAsync(
                $"v1/catlet_specifications/{SpecificationId}/versions/{EmptySpecificationVersionId}/deploy",
                new DeployCatletSpecificationRequestBody
                {
                    Variables = new Dictionary<string, string?>(),
                },
                ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Specification_is_deployed()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .PostAsJsonAsync(
                $"v1/catlet_specifications/{EmptySpecificationId}/versions/{EmptySpecificationVersionId}/deploy",
                new DeployCatletSpecificationRequestBody
                {
                    Variables = new Dictionary<string, string?>(),
                },
                ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Specification_is_deployed_when_variable_has_no_value()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .PostAsJsonAsync(
                $"v1/catlet_specifications/{SpecificationId}/versions/{SpecificationVersionId}/deploy",
                new DeployCatletSpecificationRequestBody
                {
                    Variables = new Dictionary<string, string?>
                    {
                        ["userName"] = null,
                        ["password"] = "s3cret",
                    },
                },
                ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}
