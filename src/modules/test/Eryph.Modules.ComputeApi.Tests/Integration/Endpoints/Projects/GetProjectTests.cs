using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using FluentAssertions;
using Xunit;
using ApiProject = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Project;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Projects;

public class GetProjectTests : InMemoryStateDbTestBase,
    IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private static readonly Guid UserId = Guid.NewGuid();
    private readonly WebModuleFactory<ComputeApiModule> _factory;

    public GetProjectTests(WebModuleFactory<ComputeApiModule> factory)
    {
        _factory = factory.WithApiHost(ConfigureDatabase);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
        
        var otherTenantId = Guid.NewGuid();
        await stateStore.For<Tenant>().AddAsync(
            new Tenant()
            {
                Id = otherTenantId
            });

        var projectRepo = stateStore.For<Project>();
        await projectRepo.AddAsync(
            new Project()
            {
                Id = Guid.Parse("{75715EAD-21E2-44DC-A3C4-1CDAAB387F45}"),
                Name = "dtid_norole",
                TenantId = EryphConstants.DefaultTenantId
            });
        await projectRepo.AddAsync(
            new Project()
            {
                Id = Guid.Parse("{998CC7B4-AB19-4FBA-B287-5D2A70F1DB5D}"),
                Name = "otid_norole",
                TenantId = otherTenantId
            });

        var project = new Project()
        {
            Id = Guid.Parse("{645D0AAA-2E34-4238-B0ED-65D2D307773C}"),
            Name = "dtid_role",
            TenantId = EryphConstants.DefaultTenantId
        };
        await projectRepo.AddAsync(project);
        
        var identityId = UserId.ToString().ToLowerInvariant();
        await stateStore.For<ProjectRoleAssignment>().AddAsync(
            new ProjectRoleAssignment()
            {
                Id = Guid.NewGuid(),
                Project = project,
                IdentityId = identityId,
                RoleId = EryphConstants.BuildInRoles.Reader
            });
    }

    [Theory]
    [InlineData("{75715EAD-21E2-44DC-A3C4-1CDAAB387F45}", true, "compute:read", "{C1813384-8ECB-4F17-B846-821EE515D19B}", true)]
    [InlineData("{75715EAD-21E2-44DC-A3C4-1CDAAB387F45}", false, "compute:catlets:read", "{C1813384-8ECB-4F17-B846-821EE515D19B}", true)]
    [InlineData("{75715EAD-21E2-44DC-A3C4-1CDAAB387F45}", false, "compute:projects:read", "{0F9E351B-7FB2-4CA5-B7A7-61C32FB3A7CC}", true)]
    [InlineData("{645D0AAA-2E34-4238-B0ED-65D2D307773C}", true, "compute:projects:read", "{C1813384-8ECB-4F17-B846-821EE515D19B}", true)]
    [InlineData("{645D0AAA-2E34-4238-B0ED-65D2D307773C}", true, "compute:projects:read", "{C1813384-8ECB-4F17-B846-821EE515D19B}", false)]
    public async Task Get_Returns_Existing_Project_when_authorized(
        string projectIdString, bool isAuthorized, string scope, string tenantId, bool isSuperAdmin)
    {
        var projectId = Guid.Parse(projectIdString);
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(Guid.Parse(tenantId), UserId, scope, isSuperAdmin)
            .GetAsync($"v1/projects/{projectId}");
        
        response.Should().NotBeNull();
        if (isAuthorized)
        {
            response!.StatusCode.Should().Be(HttpStatusCode.OK);
            var project = await response.Content.ReadFromJsonAsync<ApiProject>();
            project.Id.Should().Be(projectId.ToString("D"));
            project.Id.Should().NotBeNullOrWhiteSpace();

        }
        else
        {
            response!.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Get_Returns_404_If_Not_Found()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, UserId, "compute:projects:read", true)
            .GetAsync($"v1/projects/{Guid.NewGuid()}");
        
        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }
}
