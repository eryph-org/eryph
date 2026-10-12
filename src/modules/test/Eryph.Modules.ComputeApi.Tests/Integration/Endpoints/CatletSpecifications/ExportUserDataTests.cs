using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Variables;
using Eryph.Core;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.TestBase;
using Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;
using Eryph.Modules.ComputeApi.Model.V1;
using Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Catlets;
using Eryph.StateDb;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Architecture = Eryph.Core.Genetics.Architecture;
using CatletSpecification = Eryph.StateDb.Model.CatletSpecification;
using CatletSpecificationVersion = Eryph.StateDb.Model.CatletSpecificationVersion;
using CatletSpecificationVersionVariant = Eryph.StateDb.Model.CatletSpecificationVersionVariant;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.CatletSpecifications;

public class ExportUserDataTests(ITestOutputHelper outputHelper)
    : CatletSpecificationTestBase(outputHelper)
{
    private static readonly Guid ExportCatletId = Guid.NewGuid();
    private static readonly Guid ExportVmId = Guid.NewGuid();

    [Fact]
    public async Task UserData_is_exported()
    {
        var response = await PostAsync(
            SpecificationId,
            SpecificationVersionId,
            CreateBody(new Dictionary<string, string> { ["password"] = "s3cret" }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<CatletSpecificationUserData>(
            ApiJsonSerializerOptions.Options);
        var userData = result?.UserData;
        userData.Should().NotBeNull();

        userData.Should().Contain("Content-Type: text/x-shellscript");
        userData.Should().Contain("Content-Disposition: attachment; filename=\"setup.sh\"");
        userData.Should().Contain("Content-Transfer-Encoding: base64");
        userData.Should().Contain(ToBase64("user=alice"));
        userData.Should().Contain(ToBase64($"catlet={ExportCatletId}"));
        userData.Should().Contain(ToBase64($"vm={ExportVmId}"));
        // Secret fodder and secret values are included in plain text.
        userData.Should().Contain(ToBase64("pw=s3cret"));
    }

    [Fact]
    public async Task UserData_uses_the_provided_variable_values()
    {
        var response = await PostAsync(
            SpecificationId,
            SpecificationVersionId,
            CreateBody(new Dictionary<string, string>
            {
                ["userName"] = "bob",
                ["password"] = "s3cret",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CatletSpecificationUserData>(
            ApiJsonSerializerOptions.Options);
        result?.UserData.Should().Contain(ToBase64("user=bob"));
    }

    [Fact]
    public async Task UserData_is_not_exported_when_required_variable_is_missing()
    {
        var response = await PostAsync(SpecificationId, SpecificationVersionId, CreateBody(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UserData_is_not_exported_when_variable_is_invalid()
    {
        var response = await PostAsync(
            SpecificationId,
            SpecificationVersionId,
            CreateBody(new Dictionary<string, string>
            {
                ["password"] = "s3cret",
                ["port"] = "not a number",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UserData_is_not_exported_when_variable_is_unknown()
    {
        var response = await PostAsync(
            SpecificationId,
            SpecificationVersionId,
            CreateBody(new Dictionary<string, string>
            {
                ["password"] = "s3cret",
                ["unknown"] = "value",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UserData_is_not_exported_when_specification_is_in_other_project()
    {
        // The caller has write access to the "other" project but the specification
        // lives in the default project.
        await ArrangeOtherUserAccess(BuiltinRole.Contributor, OtherProjectId);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, "compute:write", false)
            .PostAsJsonAsync(
                $"v1/catlet_specifications/{SpecificationId}/versions/{SpecificationVersionId}/user_data",
                CreateBody(new Dictionary<string, string> { ["password"] = "s3cret" }),
                ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UserData_is_not_exported_when_architecture_is_not_supported()
    {
        var body = CreateBody(new Dictionary<string, string> { ["password"] = "s3cret" });
        body.Architecture = "hyperv/arm64";

        var response = await PostAsync(SpecificationId, SpecificationVersionId, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task UserData_is_not_exported_when_ids_are_missing(bool withCatletId, bool withVmId)
    {
        var body = CreateBody(new Dictionary<string, string> { ["password"] = "s3cret" });
        body.CatletId = withCatletId ? body.CatletId : null;
        body.VmId = withVmId ? body.VmId : null;

        var response = await PostAsync(SpecificationId, SpecificationVersionId, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UserData_is_not_exported_when_there_is_no_fodder()
    {
        var response = await PostAsync(EmptySpecificationId, EmptySpecificationVersionId, CreateBody(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UserData_is_not_exported_when_version_belongs_to_other_specification()
    {
        var response = await PostAsync(
            SpecificationId,
            EmptySpecificationVersionId,
            CreateBody(new Dictionary<string, string> { ["password"] = "s3cret" }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(BuiltinRole.Contributor, "compute:read", HttpStatusCode.Forbidden)]
    [InlineData(BuiltinRole.Reader, "compute:write", HttpStatusCode.NotFound)]
    public async Task UserData_is_not_exported_when_not_authorized(
        BuiltinRole role, string scope, HttpStatusCode expectedStatusCode)
    {
        await ArrangeOtherUserAccess(role, EryphConstants.DefaultProjectId);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, scope, false)
            .PostAsJsonAsync(
                $"v1/catlet_specifications/{SpecificationId}/versions/{SpecificationVersionId}/user_data",
                CreateBody(new Dictionary<string, string> { ["password"] = "s3cret" }),
                ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(expectedStatusCode);
    }

    [Fact]
    public async Task UserData_is_exported_with_write_access()
    {
        await ArrangeOtherUserAccess(BuiltinRole.Contributor, EryphConstants.DefaultProjectId);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, "compute:write", false)
            .PostAsJsonAsync(
                $"v1/catlet_specifications/{SpecificationId}/versions/{SpecificationVersionId}/user_data",
                CreateBody(new Dictionary<string, string> { ["password"] = "s3cret" }),
                ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<HttpResponseMessage> PostAsync(
        Guid specificationId,
        Guid specificationVersionId,
        ExportCatletSpecificationUserDataRequestBody body) =>
        Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .PostAsJsonAsync(
                $"v1/catlet_specifications/{specificationId}/versions/{specificationVersionId}/user_data",
                body,
                ApiJsonSerializerOptions.Options);

    private static ExportCatletSpecificationUserDataRequestBody CreateBody(
        IReadOnlyDictionary<string, string> variables) =>
        new()
        {
            Variables = variables,
            CatletId = ExportCatletId,
            VmId = ExportVmId,
        };

    private static string ToBase64(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
