using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using ApiVirtualDisk = Eryph.Modules.ComputeApi.Model.V1.VirtualDisk;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.VirtualDisks;

public class ListVirtualDisksTests(ITestOutputHelper outputHelper)
    : VirtualDiskTestBase(outputHelper)
{
    [Fact]
    public async Task Virtual_disks_are_not_included_when_not_authorized()
    {
        await ArrangeDiskWithParentAndCatlet();

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, "compute:read", false)
            .GetAsync($"v1/virtualdisks/");

        response.Should().HaveStatusCode(HttpStatusCode.OK);

        var virtualDisks = await response.Content.ReadFromJsonAsync<ListResponse<ApiVirtualDisk>>(
            options: ApiJsonSerializerOptions.Options);

        virtualDisks.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Virtual_disks_are_returned()
    {
        await ArrangeDiskWithParentAndCatlet();

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:read", true)
            .GetAsync("v1/virtualdisks/");

        response.Should().HaveStatusCode(HttpStatusCode.OK);

        var virtualDisks = await response.Content.ReadFromJsonAsync<ListResponse<ApiVirtualDisk>>(
            options: ApiJsonSerializerOptions.Options);

        virtualDisks.Value.Should().HaveCount(2);

        var virtualDisk = virtualDisks.Value.Should().ContainSingle(d => d.Id == DiskId.ToString()).Subject;
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
        virtualDisk.AttachedCatlets.Should().SatisfyRespectively(
            catlet => catlet.CatletId.Should().Be(CatletId.ToString()));

        var parentDisk = virtualDisks.Value.Should().ContainSingle(d => d.Id == ParentDiskId.ToString()).Subject;
        parentDisk.Id.Should().Be(ParentDiskId.ToString());
        parentDisk.Project.Should().NotBeNull();
        parentDisk.Project.Id.Should().Be(EryphConstants.DefaultProjectId.ToString());
        parentDisk.Project.Name.Should().Be(EryphConstants.DefaultProjectName);
        parentDisk.Project.TenantId.Should().Be(EryphConstants.DefaultTenantId.ToString());
        parentDisk.Name.Should().Be(ParentDiskName);
        parentDisk.Path.Should().Be(@"Z:\disks\test-parent-disk.vhdx");
        parentDisk.Environment.Should().Be(EnvironmentName);
        parentDisk.DataStore.Should().Be(StoreName);
        parentDisk.Location.Should().Be(LocationName);
        parentDisk.SizeBytes.Should().Be(ParentDiskSize);
        parentDisk.ParentId.Should().BeNull();
        parentDisk.ParentPath.Should().BeNull();
        parentDisk.AttachedCatlets.Should().BeEmpty();
    }

    [Fact]
    public async Task Virtual_disks_are_returned_without_paths_when_user_is_not_admin()
    {
        await ArrangeOtherUserAccess(BuiltinRole.Reader);
        await ArrangeDiskWithParentAndCatlet();

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, "compute:read", false)
            .GetAsync($"v1/virtualdisks/");

        response.Should().HaveStatusCode(HttpStatusCode.OK);

        var virtualDisks = await response.Content.ReadFromJsonAsync<ListResponse<ApiVirtualDisk>>(
            options: ApiJsonSerializerOptions.Options);

        var virtualDisk = virtualDisks.Value.Should().ContainSingle(d => d.Id == DiskId.ToString()).Subject;
        virtualDisk.Id.Should().Be(DiskId.ToString());
        virtualDisk.Path.Should().BeNull();
        virtualDisk.ParentPath.Should().BeNull();
    }
}
