using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.TestBase;
using Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Catlets;

public class ExpandCatletConfigTests(ITestOutputHelper outputHelper)
    : CatletTestBase(outputHelper)
{
    [Theory]
    [InlineData(BuiltinRole.Contributor, "compute:read", HttpStatusCode.Forbidden)]
    [InlineData(BuiltinRole.Reader, "compute:write", HttpStatusCode.NotFound)]
    public async Task Config_is_not_expanded_when_not_authorized(
        BuiltinRole role, string scope, HttpStatusCode expectedStatusCode)
    {
        await ArrangeOtherUserAccess(role, EryphConstants.DefaultProjectId);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, scope, false)
            .PostAsJsonAsync($"v1/catlets/{CatletId}/config/expand", new ExpandCatletConfigRequestBody
                {
                    Configuration = CatletConfigJsonSerializer.SerializeToElement(new CatletConfig()),
                },
                ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(expectedStatusCode);
    }

    [Fact]
    public async Task Config_is_not_expanded_when_catlet_is_in_other_project()
    {
        // Caller has write access to the "other" project but the catlet lives
        // in the default project. The spec builder hides the catlet, so the
        // shim must return 404 rather than expanding the config.
        await ArrangeOtherUserAccess(BuiltinRole.Contributor, OtherProjectId);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, "compute:write", false)
            .PostAsJsonAsync($"v1/catlets/{CatletId}/config/expand", new ExpandCatletConfigRequestBody
                {
                    Configuration = CatletConfigJsonSerializer.SerializeToElement(new CatletConfig()),
                },
                ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Config_is_not_expanded_when_payload_is_invalid()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .PostAsJsonAsync($"v1/catlets/{CatletId}/config/expand", new ExpandCatletConfigRequestBody
                {
                    Configuration = CatletConfigJsonSerializer.SerializeToElement(new CatletConfig
                    {
                        // A name containing an invalid character must be rejected by
                        // CatletConfigValidations.
                        Name = "invalid name!",
                    }),
                },
                ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Config_is_expanded()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .PostAsJsonAsync($"v1/catlets/{CatletId}/config/expand", new ExpandCatletConfigRequestBody
                {
                    Configuration = CatletConfigJsonSerializer.SerializeToElement(new CatletConfig
                    {
                        Name = "test-config",
                    }),
                    ShowSecrets = true,
                },
                ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<ExpandNewCatletConfigCommand>();
        messages.Should().SatisfyRespectively(m =>
        {
            m.Config.Should().NotBeNull();
            var config = m.Config ?? throw new InvalidOperationException("The message has no config.");
            config.Name.Should().Be("test-config");
            config.Project.Should().Be(EryphConstants.DefaultProjectName);
            m.ShowSecrets.Should().BeTrue();
        });
    }

    [Fact]
    public async Task Config_project_is_overridden_with_catlet_project()
    {
        // The shim must ignore the project specified in the payload and use the
        // catlet's project instead. This prevents a caller from expanding the
        // config in the context of a project they may not own.
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .PostAsJsonAsync($"v1/catlets/{CatletId}/config/expand", new ExpandCatletConfigRequestBody
                {
                    Configuration = CatletConfigJsonSerializer.SerializeToElement(new CatletConfig
                    {
                        Project = OtherProjectName,
                    }),
                },
                ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<ExpandNewCatletConfigCommand>();
        messages.Should()
            .SatisfyRespectively(m => { (m.Config ?? throw new InvalidOperationException("The message has no config.")).Project.Should().Be(EryphConstants.DefaultProjectName); });
    }
}
