using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.TestBase;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using ApiVirtualDisk = Eryph.Modules.ComputeApi.Model.V1.VirtualDisk;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.VirtualDisks;

public class GetVirtualDiskTests(ITestOutputHelper outputHelper)
    : VirtualDiskTestBase(outputHelper)
{
    [Fact]
    public async Task Virtual_disk_is_not_returned_when_not_authorized()
    {
        await ArrangeDiskWithParentAndCatlet();

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, "compute:read", false)
            .GetAsync($"v1/virtualdisks/{DiskId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Virtual_disk_with_parent_is_returned()
    {
        await ArrangeDiskWithParentAndCatlet();

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:read", true)
            .GetAsync($"v1/virtualdisks/{DiskId}");

        response.Should().HaveStatusCode(HttpStatusCode.OK);

        var virtualDisk = await response.Content.ReadFromJsonAsync<ApiVirtualDisk>(
            options: ApiJsonSerializerOptions.Options);

        virtualDisk.Id.Should().Be(DiskId.ToString());
        virtualDisk.Project.Should().NotBeNull();
        virtualDisk.Project.Id.Should().Be(EryphConstants.DefaultProjectId.ToString());
        virtualDisk.Project.Name.Should().Be(EryphConstants.DefaultProjectName);
        virtualDisk.Project.TenantId.Should().Be(EryphConstants.DefaultTenantId.ToString());
        virtualDisk.Name.Should().Be(DiskName);
        virtualDisk.Path.Should().Be(@"Z:\disks\test-disk.vhdx");
        virtualDisk.Environment.Should().Be(EnvironmentName);
        virtualDisk.DataStore.Should().Be(StoreName);
        virtualDisk.Location.Should().Be(LocationName);
        virtualDisk.SizeBytes.Should().Be(DiskSize);
        virtualDisk.ParentId.Should().Be(ParentDiskId.ToString());
        virtualDisk.ParentPath.Should().Be(@"Z:\disks\test-parent-disk.vhdx");
        virtualDisk.AttachedCatlets.Should().SatisfyRespectively(
            catlet => catlet.CatletId.Should().Be(CatletId.ToString()));
    }

    [Fact]
    public async Task Virtual_disk_is_returned_without_paths_when_user_is_not_admin()
    {
        await ArrangeOtherUserAccess(BuiltinRole.Reader);
        await ArrangeDiskWithParentAndCatlet();

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, "compute:read", false)
            .GetAsync($"v1/virtualdisks/{DiskId}");

        response.Should().HaveStatusCode(HttpStatusCode.OK);

        var virtualDisk = await response.Content.ReadFromJsonAsync<ApiVirtualDisk>(
            options: ApiJsonSerializerOptions.Options);
        virtualDisk.Id.Should().Be(DiskId.ToString());
        virtualDisk.Path.Should().BeNull();
        virtualDisk.ParentPath.Should().BeNull();
    }
}
