using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Core;
using Eryph.Messages.Projects;
using Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using FluentAssertions;
using Xunit;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Projects;

public class AddProjectMemberTests : InMemoryStateDbTestBase,
    IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private static readonly Guid UserId = Guid.NewGuid();
    private readonly WebModuleFactory<ComputeApiModule> _factory;

    public AddProjectMemberTests(WebModuleFactory<ComputeApiModule> factory)
    {
        _factory = factory.WithApiHost(ConfigureDatabase);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
        var otherTenantId = Guid.NewGuid();
        await stateStore.For<Tenant>().AddAsync(
            new Tenant
            {
                Id = otherTenantId
            });

        var projectRepo = stateStore.For<Project>();
        await projectRepo.AddAsync(
            new Project
            {
                Id = Guid.Parse("{E36835BB-04EB-42C8-BC36-BA75FDCBAEDD}"),
                Name = "dtid_norole",
                TenantId = EryphConstants.DefaultTenantId,
            });
        await projectRepo.AddAsync(
            new Project
            {
                Id = Guid.Parse("{D35830C0-3D25-406A-AE49-4E0E3B296D77}"),
                Name = "otid_norole",
                TenantId = otherTenantId,
            });

        var project = new Project
        {
            Id = Guid.Parse("{4A8A6FFC-48D6-4BD7-A6B1-14D5340C34EB}"),
            Name = "dtid_role",
            TenantId = EryphConstants.DefaultTenantId,
        };
        await projectRepo.AddAsync(project);
        var identityId = UserId.ToString().ToLowerInvariant();
        await stateStore.For<ProjectRoleAssignment>().AddAsync(
            new ProjectRoleAssignment()
            {
                Id = Guid.NewGuid(),
                Project = project,
                IdentityId = identityId,
                RoleId = EryphConstants.BuildInRoles.Owner,
            });
    }

    [Theory]
    [InlineData("{E36835BB-04EB-42C8-BC36-BA75FDCBAEDD}", true, "compute:write", "{C1813384-8ECB-4F17-B846-821EE515D19B}", true)]
    [InlineData("{E36835BB-04EB-42C8-BC36-BA75FDCBAEDD}", false, "compute:projects:read", "{C1813384-8ECB-4F17-B846-821EE515D19B}", true)]
    [InlineData("{D35830C0-3D25-406A-AE49-4E0E3B296D77}", false, "compute:projects:write", "{C1813384-8ECB-4F17-B846-821EE515D19B}", true)]
    [InlineData("{4A8A6FFC-48D6-4BD7-A6B1-14D5340C34EB}", true, "compute:projects:write", "{C1813384-8ECB-4F17-B846-821EE515D19B}", true)]
    [InlineData("{4A8A6FFC-48D6-4BD7-A6B1-14D5340C34EB}", true, "compute:projects:write", "{C1813384-8ECB-4F17-B846-821EE515D19B}", false)]
    public async Task Can_Add_ProjectMember_when_authorized(
        string projectIdString, bool isAuthorized, string scope, string tenantId, bool isSuperAdmin)
    {
        var memberId = Guid.NewGuid().ToString();
        var projectId = Guid.Parse(projectIdString);
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(Guid.Parse(tenantId), UserId, scope, isSuperAdmin)
            .PostAsJsonAsync($"v1/projects/{projectId}/members",
                new NewProjectMemberBody()
                {
                    RoleId = EryphConstants.BuildInRoles.Reader.ToString(),
                    CorrelationId = Guid.NewGuid(),
                    MemberId = memberId,
                });

        response.Should().NotBeNull();
        var messages = _factory.GetPendingRebusMessages<AddProjectMemberCommand>();
        if (isAuthorized)
        {
            response!.StatusCode.Should().Be(HttpStatusCode.Accepted);
            messages.Should().SatisfyRespectively(
                m =>
                {
                    m.TenantId.Should().Be(Guid.Parse(tenantId));
                    m.ProjectId.Should().Be(projectId);
                    m.MemberId.Should().Be(memberId);
                    m.RoleId.Should().Be(EryphConstants.BuildInRoles.Reader);
                });
        }
        else
        {
            response!.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
            messages.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Returns_bad_request_when_adding_system_client()
    {
        var projectId = Guid.Parse("{E36835BB-04EB-42C8-BC36-BA75FDCBAEDD}");
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, UserId, "compute:write", true)
            .PostAsJsonAsync($"v1/projects/{projectId}/members",
                new NewProjectMemberBody()
                {
                    RoleId = EryphConstants.BuildInRoles.Reader.ToString(),
                    CorrelationId = Guid.NewGuid(),
                    MemberId = "system-client"
                });

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var messages = _factory.GetPendingRebusMessages<AddProjectMemberCommand>();
        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_bad_request_when_adding_super_admin()
    {
        var projectId = Guid.Parse("{E36835BB-04EB-42C8-BC36-BA75FDCBAEDD}");
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, UserId, "compute:write", true)
            .PostAsJsonAsync($"v1/projects/{projectId}/members",
                new NewProjectMemberBody()
                {
                    RoleId = EryphConstants.BuildInRoles.Reader.ToString(),
                    CorrelationId = Guid.NewGuid(),
                    MemberId = UserId.ToString(),
                });

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var messages = _factory.GetPendingRebusMessages<AddProjectMemberCommand>();
        messages.Should().BeEmpty();
    }
}
