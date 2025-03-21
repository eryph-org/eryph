using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.VirtualDisks;

public class DeleteVirtualDiskTests(ITestOutputHelper outputHelper)
    : VirtualDiskTestBase(outputHelper)
{
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
    [InlineData(BuiltinRole.Contributor, "compute:read", HttpStatusCode.Forbidden)]
    [InlineData(BuiltinRole.Reader, "compute:write", HttpStatusCode.NotFound)]
    public async Task Virtual_disk_is_not_deleted_when_not_authorized(
        BuiltinRole role, string scope, HttpStatusCode expectedStatusCode)
    {
        await ArrangeOtherUserAccess(role);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, scope, false)
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

        var response = await Factory.CreateDefaultClient()
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

        var response = await Factory.CreateDefaultClient()
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

        var response = await Factory.CreateDefaultClient()
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

        var response = await Factory.CreateDefaultClient()
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
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .DeleteAsync($"v1/virtualdisks/{DiskId}");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var messages = Factory.GetPendingRebusMessages<DestroyVirtualDiskCommand>();
        messages.Should().SatisfyRespectively(
            m =>
            {
                m.DiskId.Should().Be(DiskId);
            });
    }
}
