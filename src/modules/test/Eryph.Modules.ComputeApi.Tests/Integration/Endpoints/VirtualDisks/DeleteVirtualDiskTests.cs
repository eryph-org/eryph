using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.VirtualDisks;

public class DeleteVirtualDiskTests : InMemoryStateDbTestBase, IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private readonly WebModuleFactory<ComputeApiModule> _factory;
    private static readonly Guid DiskId = Guid.NewGuid();

    public DeleteVirtualDiskTests(
        ITestOutputHelper outputHelper,
        WebModuleFactory<ComputeApiModule> factory)
        : base(outputHelper)
    {
        _factory = factory.WithApiHost(ConfigureDatabase);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
        await stateStore.For<VirtualDisk>().AddAsync(new VirtualDisk
        {
            Id = DiskId,
            Name = "test-disk",
            ProjectId = EryphConstants.DefaultProjectId,
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
            LastSeen = DateTimeOffset.UtcNow,
        });
    }

    [Theory]
    [InlineData(false, "compute:read", HttpStatusCode.Forbidden)]
    [InlineData(true, "compute:write", HttpStatusCode.NotFound)]
    public async Task Virtual_disk_is_not_deleted_when_not_authorized(
        bool hasReadOnlyRole, string scope, HttpStatusCode expectedStatusCode)
    {
        var notAuthorizedUserId = Guid.NewGuid().ToString();
        await WithScope(async stateStore =>
        {
            await stateStore.For<ProjectRoleAssignment>().AddAsync(
                new ProjectRoleAssignment()
                {
                    ProjectId = EryphConstants.DefaultProjectId,
                    IdentityId = notAuthorizedUserId,
                    RoleId = hasReadOnlyRole
                        ? EryphConstants.BuildInRoles.Reader
                        : EryphConstants.BuildInRoles.Contributor,
                });
            await stateStore.SaveChangesAsync();
        });

        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, notAuthorizedUserId, scope, false)
            .DeleteAsync($"v1/virtualdisks/{DiskId}");

        response.StatusCode.Should().Be(expectedStatusCode);
    }

    [Fact]
    public async Task Attached_virtual_disk_is_not_deleted()
    {
        await WithScope(async stateStore =>
        {
            await stateStore.For<Catlet>().AddAsync(
                new Catlet()
                {
                    Name = "test-catlet",
                    ProjectId = EryphConstants.DefaultProjectId,
                    Environment = EryphConstants.DefaultEnvironmentName,
                    DataStore = EryphConstants.DefaultDataStoreName,
                    Drives =
                    [
                        new CatletDrive()
                        {
                            Id = Guid.NewGuid().ToString(),
                            AttachedDiskId = DiskId,
                        },
                    ],
                });
            await stateStore.SaveChangesAsync();
        });

        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .DeleteAsync($"v1/virtualdisks/{DiskId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            options: ApiJsonSerializerOptions.Options);
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Be("The disk is attached to a virtual machine and cannot be deleted.");
    }

    [Fact]
    public async Task Virtual_disk_with_children_is_not_deleted()
    {
        await WithScope(async stateStore =>
        {
            await stateStore.For<VirtualDisk>().AddAsync(
                new VirtualDisk()
                {
                    Id = Guid.NewGuid(),
                    Name = "child-disk",
                    ProjectId = EryphConstants.DefaultProjectId,
                    Environment = EryphConstants.DefaultEnvironmentName,
                    DataStore = EryphConstants.DefaultDataStoreName,
                    ParentId = DiskId,
                });
            await stateStore.SaveChangesAsync();
        });

        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .DeleteAsync($"v1/virtualdisks/{DiskId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            options: ApiJsonSerializerOptions.Options);
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Be("The disk has children and cannot be deleted.");
    }

    [Fact]
    public async Task Virtual_disk_from_gene_pool_is_not_deleted()
    {
        var genePoolDiskId = Guid.NewGuid();
        await WithScope(async stateStore =>
        {
            await stateStore.For<VirtualDisk>().AddAsync(
                new VirtualDisk()
                {
                    Id = genePoolDiskId,
                    Name = "sda",
                    ProjectId = EryphConstants.DefaultProjectId,
                    Environment = EryphConstants.DefaultEnvironmentName,
                    DataStore = EryphConstants.DefaultDataStoreName,
                    GeneSet = "dbosoft/test-os/20000101",
                    GeneName = "sda",
                    GeneArchitecture = "hyperv/amd64",
                });
            await stateStore.SaveChangesAsync();
        });

        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .DeleteAsync($"v1/virtualdisks/{genePoolDiskId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            options: ApiJsonSerializerOptions.Options);
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Be("The disk belongs to the gene pool and cannot be deleted.");
    }

    [Fact]
    public async Task Frozen_virtual_disk_is_not_deleted()
    {
        await WithScope(async stateStore =>
        {
            var disk = await stateStore.For<VirtualDisk>().GetByIdAsync(DiskId);
            disk!.Frozen = true;
            await stateStore.SaveChangesAsync();
        });

        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .DeleteAsync($"v1/virtualdisks/{DiskId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            options: ApiJsonSerializerOptions.Options);
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Be("The configuration of the disk is frozen. The disk cannot be deleted.");
    }

    [Fact]
    public async Task Virtual_disk_is_deleted()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .DeleteAsync($"v1/virtualdisks/{DiskId}");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var messages = _factory.GetPendingRebusMessages<DestroyVirtualDiskCommand>();
        messages.Should().SatisfyRespectively(
            m =>
            {
                m.DiskId.Should().Be(DiskId);
            });
    }

    private async Task WithScope(Func<IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(stateStore);
    }
}
