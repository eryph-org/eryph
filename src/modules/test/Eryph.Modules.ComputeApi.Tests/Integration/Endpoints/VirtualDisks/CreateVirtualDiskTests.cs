using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Core;
using Eryph.Messages.Resources.Disks;
using Eryph.Modules.ComputeApi.Endpoints.V1.VirtualDisks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using FluentAssertions;
using Xunit;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.VirtualDisks;

public class CreateVirtualDiskTests : InMemoryStateDbTestBase, IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private readonly WebModuleFactory<ComputeApiModule> _factory;

    private const string DiskName = "test-disk";
    private const int DiskSize = 5;
    private const string LocationName = "test-location";
    private const string EnvironmentName = "test-environment";
    private const string StoreName = "test-store";

    public CreateVirtualDiskTests(WebModuleFactory<ComputeApiModule> factory)
    {
        _factory = factory.WithApiHost(ConfigureDatabase);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
    }

    [Theory]
    [InlineData(false, "compute:read", HttpStatusCode.Forbidden)]
    [InlineData(true, "compute:write", HttpStatusCode.Forbidden)]
    public async Task Virtual_disk_is_not_created_when_not_authorized(
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
            .PostAsJsonAsync("v1/virtualdisks", new NewVirtualDiskRequest
            {
                ProjectId = EryphConstants.DefaultProjectId,
                Name = DiskName,
                Size = DiskSize,
                Location = LocationName,
            });

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

        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .PostAsJsonAsync("v1/virtualdisks", new NewVirtualDiskRequest
            {
                ProjectId = EryphConstants.DefaultProjectId,
                Name = DiskName,
                Location = LocationName,
                Size = DiskSize,
                Environment = EnvironmentName,
                Store = StoreName,
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Virtual_disk_is_created()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .PostAsJsonAsync("v1/virtualdisks", new NewVirtualDiskRequest
            {
                ProjectId = EryphConstants.DefaultProjectId,
                Name = DiskName,
                Location = LocationName,
                Size = 5,
                Environment = EnvironmentName,
                Store = StoreName,
            });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = _factory.GetPendingRebusMessages<CreateVirtualDiskCommand>();
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

    private async Task WithScope(Func<IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(stateStore);
    }
}
