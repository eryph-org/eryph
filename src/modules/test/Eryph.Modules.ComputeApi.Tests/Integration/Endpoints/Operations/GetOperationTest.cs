using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Core;
using Eryph.StateDb.Model;
using FluentAssertions;
using Xunit;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Operations;

public class GetOperationTest : IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private readonly WebModuleFactory<ComputeApiModule> _factory;

    public GetOperationTest(WebModuleFactory<ComputeApiModule> factory)
    {
        _factory = factory;
    }

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();


    private static readonly Project Project = new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        ProjectRoles = new List<ProjectRoleAssignment>
        {
            new()
            {
                RoleId = EryphConstants.BuildInRoles.Reader,
                IdentityId = UserId.ToString(),
                Id = Guid.NewGuid()
            }
        }
    };

    private static readonly OperationModel ExistingOperation = new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        Projects = new List<OperationProjectModel>(new[] {new OperationProjectModel
        {
            Id = Guid.NewGuid(),
            Project = Project,
        }})

    };

    private static readonly OperationModel CrossOperation = new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        Projects = new List<OperationProjectModel>(new[] {
            new OperationProjectModel
            {
                Id = Guid.NewGuid(),
                Project = Project,
            },
            new OperationProjectModel
            {
                Id = Guid.NewGuid(),
                Project = new Project{Id = Guid.NewGuid()},
            }})

    };

    private WebModuleFactory<ComputeApiModule> FactoryWithOperation()
    {
        return _factory.WithApiHost().SetupStateStore(context =>
        {
            context.Operations.Add(ExistingOperation);
            context.Operations.Add(CrossOperation);
            context.Projects.Add(new Project
            {
                TenantId = ExistingOperation.TenantId,
                Id = Guid.NewGuid(),

            });


        });

    }

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

    [Fact]
    public async Task Get_Returns_Existing_Operation()
    {
        var claims = CreateClaims("compute:project:read", TenantId, false);

        var result = await FactoryWithOperation().CreateDefaultClient()
            .SetFakeBearerToken(claims)
            .GetFromJsonAsync<OperationModel>($"v1/operations/{ExistingOperation.Id}");
        result.Should().NotBeNull();
        result!.Id.Should().Be(ExistingOperation.Id.ToString());


    }

    [Fact]
    public async Task Get_Returns_404_If_Not_Found()
    {

        var opId = Guid.NewGuid();
        var claims = CreateClaims("compute:project:read", TenantId, false);
        var response = await FactoryWithOperation().CreateDefaultClient()
            .SetFakeBearerToken(claims)
            .GetAsync($"v1/operations/{opId}");
        response.Should().HaveStatusCode(HttpStatusCode.NotFound);

    }

    [Fact]
    public async Task Get_Returns_404_If_non_admin_and_multi_project_op()
    {

        var claims = CreateClaims("compute:project:read", TenantId, false);
        var response = await FactoryWithOperation().CreateDefaultClient()
            .SetFakeBearerToken(claims)
            .GetAsync($"v1/operations/{CrossOperation.Id}");
        response.Should().HaveStatusCode(HttpStatusCode.NotFound);

    }

    [Fact]
    public async Task Get_Returns_cross_Operation_as_admin()
    {
        var claims = CreateClaims("compute:project:read", TenantId, true);

        var result = await FactoryWithOperation().CreateDefaultClient()
            .SetFakeBearerToken(claims)
            .GetFromJsonAsync<OperationModel>($"v1/operations/{CrossOperation.Id}");
        result.Should().NotBeNull();
        result!.Id.Should().Be(CrossOperation.Id.ToString());


    }
}