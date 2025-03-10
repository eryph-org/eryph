using Dbosoft.Hosuto.Modules.Testing;
using Eryph.StateDb.TestBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Xunit;
using Xunit.Abstractions;
using Eryph.Core;
using Eryph.Modules.AspNetCore.ApiProvider;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ApiVirtualDisk = Eryph.Modules.ComputeApi.Model.V1.VirtualDisk;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.VirtualDisks;

public class ListVirtualDisksTests : VirtualDiskTestBase
{
    public ListVirtualDisksTests(
        ITestOutputHelper outputHelper,
        WebModuleFactory<ComputeApiModule> factory)
        : base(outputHelper, factory)
    {
        
    }

    [Fact]
    public async Task Virtual_disk_with_parent_is_returned()
    {
        await ArrangeDiskWithParentAndCatlet();

        // TODO test list endpoint
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
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
    }

    [Fact]
    public async Task Virtual_disk_is_returned_without_paths_when_user_is_not_admin()
    {
        await ArrangeOtherUser(BuiltinRole.Reader);
        await ArrangeDiskWithParentAndCatlet();

        // TODO test list endpoint
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, "compute:read", false)
            .GetAsync($"v1/virtualdisks/{DiskId}");

        response.Should().HaveStatusCode(HttpStatusCode.OK);

        var virtualDisk = await response.Content.ReadFromJsonAsync<ApiVirtualDisk>(
            options: ApiJsonSerializerOptions.Options);
        virtualDisk.Id.Should().Be(DiskId.ToString());
        virtualDisk.Path.Should().BeNull();
    }
}
