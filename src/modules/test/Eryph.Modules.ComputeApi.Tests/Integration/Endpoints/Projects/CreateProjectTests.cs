﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Core;
using Eryph.Messages.Projects;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.ComputeApi.Endpoints.V1.Projects;
using Eryph.StateDb;
using Eryph.StateDb.TestBase;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Projects;

public class CreateProjectTests : InMemoryStateDbTestBase,
    IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private static readonly Guid UserId = Guid.NewGuid();
    private readonly WebModuleFactory<ComputeApiModule> _factory;

    public CreateProjectTests(
        ITestOutputHelper outputHelper,
        WebModuleFactory<ComputeApiModule> factory)
        : base(outputHelper)
    {
        _factory = factory.WithApiHost(ConfigureDatabase);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
    }

    [Fact]
    public async Task System_client_is_not_assigned_to_new_project()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .PostAsJsonAsync("v1/projects",
                new NewProjectRequest()
                {
                    CorrelationId = Guid.NewGuid(),
                    Name = "test-project",
                },
                options: ApiJsonSerializerOptions.Options);

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var messages = _factory.GetPendingRebusMessages<CreateProjectCommand>();
        messages.Should().SatisfyRespectively(
            m =>
            {
                m.TenantId.Should().Be(EryphConstants.DefaultTenantId);
                m.ProjectName.Should().Be("test-project");
                m.IdentityId.Should().BeNull();
            });
    }

    [Fact]
    public async Task Super_admin_is_not_assigned_to_new_project()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, UserId, "compute:write", true)
            .PostAsJsonAsync("v1/projects",
                new NewProjectRequest()
                {
                    CorrelationId = Guid.NewGuid(),
                    Name = "test-project",
                },
                options: ApiJsonSerializerOptions.Options);

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = _factory.GetPendingRebusMessages<CreateProjectCommand>();
        messages.Should().SatisfyRespectively(
            m =>
            {
                m.TenantId.Should().Be(EryphConstants.DefaultTenantId);
                m.ProjectName.Should().Be("test-project");
                m.IdentityId.Should().BeNull();
            });
    }

    [Fact]
    public async Task Normal_user_is_assigned_as_owner_to_new_project()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, UserId, "compute:write", false)
            .PostAsJsonAsync("v1/projects",
                new NewProjectRequest()
                {
                    CorrelationId = Guid.NewGuid(),
                    Name = "test-project",
                },
                options: ApiJsonSerializerOptions.Options);

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = _factory.GetPendingRebusMessages<CreateProjectCommand>();
        messages.Should().SatisfyRespectively(
            m =>
            {
                m.TenantId.Should().Be(EryphConstants.DefaultTenantId);
                m.ProjectName.Should().Be("test-project");
                m.IdentityId.Should().Be(UserId.ToString());
            });
    }
}
