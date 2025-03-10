using Dbosoft.Hosuto.Modules.Testing;
using Eryph.StateDb;
using Eryph.StateDb.TestBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.VirtualDisks;

public abstract class VirtualDiskTestBase : InMemoryStateDbTestBase, IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    protected readonly WebModuleFactory<ComputeApiModule> Factory;

    protected static readonly Guid OtherClientId = Guid.NewGuid();
    protected static readonly Guid CatletId = Guid.NewGuid();
    protected static readonly Guid CatletMetadataId = Guid.NewGuid();

    protected const string LocationName = "test-location";
    protected const string EnvironmentName = "test-environment";
    protected const string StoreName = "test-store";

    protected static readonly Guid DiskId = Guid.NewGuid();
    protected const string DiskName = "test-disk";
    protected const string DiskPath = @"Z:\disks\test-disk.vhdx";
    protected const int DiskSize = 42;

    protected static readonly Guid ParentDiskId = Guid.NewGuid();
    protected const string ParentDiskName = "test-parent-disk";
    protected const string ParentDiskPath = @"Z:\disks\test-parent-disk.vhdx";

    protected VirtualDiskTestBase(
        ITestOutputHelper outputHelper,
        WebModuleFactory<ComputeApiModule> factory)
        : base(outputHelper)
    {
        Factory = factory.WithApiHost(ConfigureDatabase);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
    }

    protected async Task ArrangeDiskWithParentAndCatlet()
    {
        await ArrangeCatlet();
        await WithScope(async stateStore =>
        {
            var parentDisk = await stateStore.For<VirtualDisk>().AddAsync(
                new VirtualDisk()
                {
                    Id = ParentDiskId,
                    ProjectId = EryphConstants.DefaultProjectId,
                    Name = ParentDiskName,
                    Path = @"Z:\disks",
                    FileName = "test-parent-disk.vhdx",
                    Environment = EnvironmentName,
                    DataStore = StoreName,
                    StorageIdentifier = LocationName,
                });

            await stateStore.For<VirtualDisk>().AddAsync(
                new VirtualDisk()
                {
                    Id = DiskId,
                    ProjectId = EryphConstants.DefaultProjectId,
                    Name = DiskName,
                    Path = @"Z:\disks",
                    FileName = "test-disk.vhdx",
                    Environment = EnvironmentName,
                    DataStore = StoreName,
                    StorageIdentifier = LocationName,
                    Parent = parentDisk,
                    SizeBytes = DiskSize,
                    AttachedDrives =
                    [
                        new CatletDrive
                        {
                            Id = Guid.NewGuid().ToString(),
                            CatletId = CatletId,
                        },
                    ],
                });
            await stateStore.SaveChangesAsync();
        });
    }

    protected async Task ArrangeCatlet()
    {
        await WithScope(async stateStore =>
        {
            var metadata = await stateStore.For<CatletMetadata>().AddAsync(new CatletMetadata
            {
                Id = CatletMetadataId,
                Metadata = "test-catlet-metadata",
            });

            await stateStore.For<Catlet>().AddAsync(new Catlet
            {
                Id = CatletId,
                ProjectId = EryphConstants.DefaultProjectId,
                MetadataId = metadata.Id,
                Name = "test-catlet",
                DataStore = EryphConstants.DefaultDataStoreName,
                Environment = EryphConstants.DefaultEnvironmentName,
            });
            await stateStore.SaveChangesAsync();
        });
    }

    protected async Task ArrangeOtherUser(BuiltinRole role)
    {
        await WithScope(async stateStore =>
        {
            await stateStore.For<ProjectRoleAssignment>().AddAsync(
                new ProjectRoleAssignment()
                {
                    ProjectId = EryphConstants.DefaultProjectId,
                    IdentityId = OtherClientId.ToString(),
                    RoleId = role.ToRoleId(),
                });
            await stateStore.SaveChangesAsync();
        });
    }

    protected async Task WithScope(Func<IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(stateStore);
    }
}