using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Resources.Disks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.ComputeApi.Endpoints.V1.VirtualDisks;
using Eryph.StateDb.Model;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.VirtualDisks;

public class CreateVirtualDiskTests(ITestOutputHelper outputHelper)
    : VirtualDiskTestBase(outputHelper)
{
    [Theory]
    [InlineData(BuiltinRole.Contributor, "compute:read", HttpStatusCode.Forbidden)]
    [InlineData(BuiltinRole.Reader, "compute:write", HttpStatusCode.Forbidden)]
    public async Task Virtual_disk_is_not_created_when_not_authorized(
        BuiltinRole role,
        string scope,
        HttpStatusCode expectedStatusCode)
    {
        await ArrangeOtherUserAccess(role);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, scope, false)
            .PostAsJsonAsync("v1/virtualdisks", new NewVirtualDiskRequest
            {
                ProjectId = EryphConstants.DefaultProjectId.ToString(),
                Name = DiskName,
                Size = DiskSize,
                Location = LocationName,
            },
            options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(expectedStatusCode);
    }

    [Fact]
    public async Task Virtual_disk_is_not_created_when_disk_already_exists()
    {
        await WithScope(async stateStore =>
        {
            await stateStore.For<VirtualDisk>().AddAsync(
                new VirtualDisk()
                {
                    ProjectId = EryphConstants.DefaultProjectId,
                    Name = DiskName,
                    Environment = EnvironmentName,
                    DataStore = StoreName,
                    StorageIdentifier = LocationName,
                });
            await stateStore.SaveChangesAsync();
        });

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .PostAsJsonAsync("v1/virtualdisks", new NewVirtualDiskRequest
            {
                ProjectId = EryphConstants.DefaultProjectId.ToString(),
                Name = DiskName,
                Location = LocationName,
                Size = DiskSize,
                Environment = EnvironmentName,
                Store = StoreName,
            },
            options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Virtual_disk_with_default_values_is_not_created_when_disk_already_exists()
    {
        await WithScope(async stateStore =>
        {
            await stateStore.For<VirtualDisk>().AddAsync(
                new VirtualDisk()
                {
                    ProjectId = EryphConstants.DefaultProjectId,
                    Name = DiskName,
                    Environment = EryphConstants.DefaultEnvironmentName,
                    DataStore = EryphConstants.DefaultDataStoreName,
                    StorageIdentifier = LocationName,
                });
            await stateStore.SaveChangesAsync();
        });

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .PostAsJsonAsync("v1/virtualdisks", new NewVirtualDiskRequest
            {
                ProjectId = EryphConstants.DefaultProjectId.ToString(),
                Name = DiskName,
                Location = LocationName,
                Size = DiskSize,
            },
            options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Virtual_disk_is_created()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .PostAsJsonAsync("v1/virtualdisks", new NewVirtualDiskRequest
            {
                ProjectId = EryphConstants.DefaultProjectId.ToString(),
                Name = DiskName,
                Location = LocationName,
                Size = DiskSize,
                Environment = EnvironmentName,
                Store = StoreName,
            },
            options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<CreateVirtualDiskCommand>();
        messages.Should().SatisfyRespectively(
            m =>
            {
                m.ProjectId.Should().Be(EryphConstants.DefaultProjectId);
                m.Name.Should().Be(DiskName);
                m.Size.Should().Be(DiskSize);
                m.StorageIdentifier.Should().Be(LocationName);
                m.Environment.Should().Be(EnvironmentName);
                m.DataStore.Should().Be(StoreName);
            });
    }

    [Fact]
    public async Task Virtual_disk_is_created_with_default_values()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .PostAsJsonAsync("v1/virtualdisks", new NewVirtualDiskRequest
            {
                ProjectId = EryphConstants.DefaultProjectId.ToString(),
                Name = DiskName,
                Location = LocationName,
                Size = DiskSize,
            },
            options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<CreateVirtualDiskCommand>();
        messages.Should().SatisfyRespectively(
            m =>
            {
                m.ProjectId.Should().Be(EryphConstants.DefaultProjectId);
                m.Name.Should().Be(DiskName);
                m.Size.Should().Be(DiskSize);
                m.StorageIdentifier.Should().Be(LocationName);
                m.Environment.Should().Be(null);
                m.DataStore.Should().Be(null);
            });
    }
}
