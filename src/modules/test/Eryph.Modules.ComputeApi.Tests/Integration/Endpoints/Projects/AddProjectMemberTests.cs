using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Core;
using Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using Xunit;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Projects;

public class AddProjectMemberTests : IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private readonly WebModuleFactory<ComputeApiModule> _factory;

    public AddProjectMemberTests(WebModuleFactory<ComputeApiModule> factory)
    {
        _factory = factory.WithApiHost().WithModuleConfiguration(o =>
        {
            o.Configure(sp =>
            {
                var container = sp.Services.GetRequiredService<Container>();
                using var scope = AsyncScopedLifestyle.BeginScope(container);

                var stateStore = scope.GetInstance<StateStoreContext>();

                stateStore.Projects.Add(
                    new StateDb.Model.Project
                    {
                        Id = Guid.Parse("{E36835BB-04EB-42C8-BC36-BA75FDCBAEDD}"),
                        Name = "dtid_norole",
                        TenantId = EryphConstants.DefaultTenantId
                    });
                stateStore.Projects.Add(
                    new StateDb.Model.Project
                    {
                        Id = Guid.Parse("{D35830C0-3D25-406A-AE49-4E0E3B296D77}"),
                        Name = "otid_norole",
                        TenantId = new Guid()
                    });

                var project = new StateDb.Model.Project
                {
                    Id = Guid.Parse("{4A8A6FFC-48D6-4BD7-A6B1-14D5340C34EB}"),
                    Name = "dtid_role",
                    TenantId = EryphConstants.DefaultTenantId
                };
                stateStore.Projects.Add(project);
                var identityId = UserId.ToString().ToLowerInvariant();
                stateStore.ProjectRoles.Add(new ProjectRoleAssignment()
                {
                    Id = Guid.NewGuid(),
                    Project = project,
                    IdentityId = identityId,
                    RoleId = EryphConstants.BuildInRoles.Owner
                });

                stateStore.SaveChanges();
            });
        });


    }

    private static readonly Guid UserId = Guid.NewGuid();

    private static Dictionary<string, object> CreateClaims(string scope, Guid tenantId, bool isSuperAdmin)
    {
        return new Dictionary<string, object>
        {
            { "iss", "fake"},
            { "sub", UserId},
            { "scope", scope },
            { "tid", tenantId},
            { ClaimTypes.Role, isSuperAdmin? EryphConstants.SuperAdminRole : Guid.NewGuid() }
        };
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
        var claims = CreateClaims(scope, Guid.Parse(tenantId), isSuperAdmin);
        var projectId = Guid.Parse(projectIdString);
        var response = await _factory.CreateDefaultClient()
            .SetFakeBearerToken(claims)
            .PostAsJsonAsync($"v1/projects/{projectId}/members",
                new NewProjectMemberBody()
                {
                    RoleId = EryphConstants.BuildInRoles.Reader,
                    CorrelationId = Guid.NewGuid(),
                    MemberId = Guid.NewGuid().ToString()
                });
        response.Should().NotBeNull();

        if (isAuthorized)
        {
            response!.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }
        else
        {
            response!.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
        }

    }

}