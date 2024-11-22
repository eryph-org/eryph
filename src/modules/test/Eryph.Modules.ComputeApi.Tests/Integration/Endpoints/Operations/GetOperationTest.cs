using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Core;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using ApiOperation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Operations;

public class GetOperationTest : InMemoryStateDbTestBase,
    IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static readonly Guid ExistingOperationId = Guid.NewGuid();
    private static readonly Guid CrossOperationId = Guid.NewGuid();

    private readonly WebModuleFactory<ComputeApiModule> _factory;

    public GetOperationTest(
        ITestOutputHelper outputHelper,
        WebModuleFactory<ComputeApiModule> factory)
        : base(outputHelper)
    {
        _factory = factory.WithApiHost(ConfigureDatabase);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await stateStore.For<Tenant>().AddAsync(
            new Tenant
            {
                Id = TenantId,
            });

        var projectRepo = stateStore.For<Project>();
        await projectRepo.AddAsync(
            new Project()
            {
                Id = ProjectId,
                TenantId = TenantId,
                Name = "test-project",
                ProjectRoles =
                [
                    new ProjectRoleAssignment()
                    {
                        Id = Guid.NewGuid(),
                        RoleId = EryphConstants.BuildInRoles.Reader,
                        IdentityId = UserId.ToString(),
                    },
                ],
            });
        var otherProjectId = Guid.NewGuid();
        await projectRepo.AddAsync(
            new Project()
            {
                Id = otherProjectId,
                TenantId = TenantId,
                Name = "other-test-project",
            });


        var operationRepo = stateStore.For<OperationModel>();
        await operationRepo.AddAsync(
            new OperationModel
            {
                Id = ExistingOperationId,
                TenantId = TenantId,
                LastUpdated = DateTimeOffset.UtcNow,
                Projects =
                [
                    new OperationProjectModel
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = ProjectId,
                    },
                ],
            });

        await operationRepo.AddAsync(
            new OperationModel()
            {
                Id = CrossOperationId,
                TenantId = TenantId,
                LastUpdated = DateTimeOffset.UtcNow,
                Projects =
                [
                    new OperationProjectModel
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = ProjectId,
                    },
                    new OperationProjectModel
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = otherProjectId,
                    },
                ],
            });
    }

    [Fact]
    public async Task Get_Returns_Existing_Operation()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(TenantId, UserId, "compute:project:read", false)
            .GetAsync($"v1/operations/{ExistingOperationId}");

        response.Should().HaveStatusCode(HttpStatusCode.OK);

        var operation = await response.Content.ReadFromJsonAsync<ApiOperation>(
            options: ApiJsonSerializerOptions.Options);

        operation.Should().NotBeNull();
        operation!.Id.Should().Be(ExistingOperationId.ToString());
    }

    [Fact]
    public async Task Get_Returns_404_If_Not_Found()
    {
        var opId = Guid.NewGuid();
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(TenantId, UserId, "compute:project:read", false)
            .GetAsync($"v1/operations/{opId}");

        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Returns_404_If_non_admin_and_multi_project_op()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(TenantId, UserId, "compute:project:read", false)
            .GetAsync($"v1/operations/{CrossOperationId}");
        
        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Returns_cross_Operation_as_admin()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(TenantId, UserId, "compute:project:read", true)
            .GetAsync($"v1/operations/{CrossOperationId}");

        response.Should().HaveStatusCode(HttpStatusCode.OK);

        var operation =  await response.Content.ReadFromJsonAsync<ApiOperation>(
            options: ApiJsonSerializerOptions.Options);

        operation.Should().NotBeNull();
        operation!.Id.Should().Be(CrossOperationId.ToString());
    }
}
