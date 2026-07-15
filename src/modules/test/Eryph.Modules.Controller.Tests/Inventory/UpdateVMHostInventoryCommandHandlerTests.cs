using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.Inventory;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using LanguageExt;
using static LanguageExt.Prelude;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rebus.Pipeline;
using SimpleInjector;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.Inventory;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlUpdateVMHostInventoryCommandHandlerTests(
    ITestOutputHelper outputHelper, MySqlFixture databaseFixture)
    : UpdateVMHostInventoryCommandHandlerTests(outputHelper, databaseFixture);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteUpdateVMHostInventoryCommandHandlerTests(
    ITestOutputHelper outputHelper, SqliteFixture databaseFixture)
    : UpdateVMHostInventoryCommandHandlerTests(outputHelper, databaseFixture);

/// <summary>
/// Verifies that the inventory never rewrites where a catlet lives. Its environment and site are
/// decided when it is deployed and are part of its identity; the value derived from the VM's path is
/// only a second observation of the same fact, so it must not overwrite the stored one.
/// </summary>
public abstract class UpdateVMHostInventoryCommandHandlerTests(
    ITestOutputHelper outputHelper, IDatabaseFixture databaseFixture)
    : StateDbTestBase(databaseFixture, outputHelper)
{
    private const string HostName = "testhost";

    private static readonly Guid CatletId = new("a1b2c3d4-0000-4000-8000-000000000001");
    private static readonly Guid MetadataId = new("a1b2c3d4-0000-4000-8000-000000000002");
    private static readonly Guid VmId = new("a1b2c3d4-0000-4000-8000-000000000003");
    private static readonly Guid OtherSiteId = new("a1b2c3d4-0000-4000-8000-000000000004");

    // Metadata whose catlet does not exist: a VM eryph knows but has not recorded yet.
    private static readonly Guid OrphanedCatletId = new("a1b2c3d4-0000-4000-8000-000000000005");
    private static readonly Guid OrphanedMetadataId = new("a1b2c3d4-0000-4000-8000-000000000006");
    private static readonly Guid OrphanedVmId = new("a1b2c3d4-0000-4000-8000-000000000007");

    private readonly StubRegistry _registry = new();

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        await stateStore.For<Site>().AddAsync(new Site { Id = OtherSiteId, Name = "elsewhere" });

        await stateStore.For<CatletMetadata>().AddAsync(new CatletMetadata
        {
            Id = MetadataId,
            CatletId = CatletId,
            VmId = VmId,
            Metadata = new CatletMetadataContent(),
        });

        await stateStore.For<Catlet>().AddAsync(new Catlet
        {
            Id = CatletId,
            ProjectId = EryphConstants.DefaultProjectId,
            MetadataId = MetadataId,
            VmId = VmId,
            Name = "test-catlet",
            // The catlet was deployed into 'staging', which is realized elsewhere.
            Environment = "staging",
            SiteId = OtherSiteId,
            DataStore = EryphConstants.DefaultDataStoreName,
            LastSeen = DateTimeOffset.MinValue,
            LastSeenState = DateTimeOffset.MinValue,
        });
    }

    [Fact]
    public async Task Environment_and_site_of_an_existing_catlet_are_not_rewritten_from_the_path()
    {
        // The host reports the VM as being in the default environment: the path stopped resolving to
        // 'staging', e.g. because the datastore configuration changed.
        await Handle(environment: EryphConstants.DefaultEnvironmentName);

        await WithScope(async stateStore =>
        {
            var catlet = await stateStore.For<Catlet>().GetByIdAsync(CatletId);

            catlet!.Environment.Should().Be("staging");
            catlet.SiteId.Should().Be(OtherSiteId);
        });
    }

    [Fact]
    public async Task Environment_of_an_existing_catlet_survives_an_unattributable_path()
    {
        await Handle(environment: null);

        await WithScope(async stateStore =>
        {
            var catlet = await stateStore.For<Catlet>().GetByIdAsync(CatletId);

            // Never "" — the environment is part of the catlet's identity.
            catlet!.Environment.Should().Be("staging");
            catlet.SiteId.Should().Be(OtherSiteId);
        });
    }

    [Fact]
    public async Task Other_inventory_data_is_still_applied()
    {
        await Handle(environment: null, vmName: "renamed-catlet");

        await WithScope(async stateStore =>
        {
            var catlet = await stateStore.For<Catlet>().GetByIdAsync(CatletId);

            // An unattributable path must not stop the rest of the inventory from being recorded.
            catlet!.Name.Should().Be("renamed-catlet");
        });
    }

    [Fact]
    public async Task A_first_seen_vm_with_an_unattributable_path_is_skipped_not_inserted()
    {
        // Metadata exists but the catlet does not, so this VM would be recorded for the first time.
        // Its environment is only knowable from its path, and the path cannot be attributed — it
        // must be skipped rather than inserted with an empty environment, which would be an
        // unusable identity.
        await WithScope(async stateStore =>
        {
            await stateStore.For<CatletMetadata>().AddAsync(new CatletMetadata
            {
                Id = OrphanedMetadataId,
                CatletId = OrphanedCatletId,
                VmId = OrphanedVmId,
                Metadata = new CatletMetadataContent(),
            });
            await stateStore.SaveChangesAsync();
        });

        await Handle(environment: null, vmId: OrphanedVmId, metadataId: OrphanedMetadataId);

        await WithScope(async stateStore =>
        {
            var catlets = await stateStore.For<Catlet>().ListAsync();

            // Only the pre-existing catlet; the unattributable one was not inserted.
            catlets.Should().ContainSingle().Which.Id.Should().Be(CatletId);
        });
    }

    [Fact]
    public async Task A_copied_vm_whose_name_is_taken_is_skipped_not_inserted()
    {
        // A VM copied on the host keeps its name, so it collides with the catlet it was copied
        // from. The insert would only fail when the unit of work commits, taking the whole host's
        // inventory round with it and repeating on every retry.
        await WithScope(async stateStore =>
        {
            await stateStore.For<CatletMetadata>().AddAsync(new CatletMetadata
            {
                Id = OrphanedMetadataId,
                CatletId = OrphanedCatletId,
                // A different VM id: this is the copy-detection path.
                VmId = Guid.NewGuid(),
                Metadata = new CatletMetadataContent(),
            });
            await stateStore.SaveChangesAsync();
        });

        await Handle(
            environment: "staging",
            vmName: "test-catlet",
            vmId: OrphanedVmId,
            metadataId: OrphanedMetadataId);

        await WithScope(async stateStore =>
        {
            var catlets = await stateStore.For<Catlet>().ListAsync();

            catlets.Should().ContainSingle().Which.Id.Should().Be(CatletId);
        });
    }

    [Fact]
    public async Task A_rename_which_collides_keeps_the_stored_name()
    {
        // A second catlet whose name the existing one is about to be renamed to.
        await WithScope(async stateStore =>
        {
            await stateStore.For<Catlet>().AddAsync(new Catlet
            {
                Id = Guid.NewGuid(),
                ProjectId = EryphConstants.DefaultProjectId,
                SiteId = OtherSiteId,
                Name = "taken",
                Environment = "staging",
                DataStore = EryphConstants.DefaultDataStoreName,
            });
            await stateStore.SaveChangesAsync();
        });

        await Handle(environment: "staging", vmName: "taken");

        await WithScope(async stateStore =>
        {
            var catlet = await stateStore.For<Catlet>().GetByIdAsync(CatletId);

            catlet!.Name.Should().Be("test-catlet");
        });
    }

    [Fact]
    public async Task A_name_observed_on_the_host_is_stored_lower_cased()
    {
        // Every lookup compares against the lower-cased name, so the uniqueness of a catlet's name
        // must not depend on the database's collation.
        await Handle(environment: "staging", vmName: "Renamed-CATLET");

        await WithScope(async stateStore =>
        {
            var catlet = await stateStore.For<Catlet>().GetByIdAsync(CatletId);

            catlet!.Name.Should().Be("renamed-catlet");
        });
    }

    [Fact]
    public async Task A_first_seen_vm_with_an_attributable_path_is_inserted()
    {
        // The counterpart: the same VM IS recorded once its path resolves, so the skip above is the
        // unattributable path and not the metadata check in front of it.
        await WithScope(async stateStore =>
        {
            await stateStore.For<CatletMetadata>().AddAsync(new CatletMetadata
            {
                Id = OrphanedMetadataId,
                CatletId = OrphanedCatletId,
                VmId = OrphanedVmId,
                Metadata = new CatletMetadataContent(),
            });
            await stateStore.SaveChangesAsync();
        });

        await Handle(
            environment: EryphConstants.DefaultEnvironmentName,
            vmName: "discovered-catlet",
            vmId: OrphanedVmId,
            metadataId: OrphanedMetadataId);

        await WithScope(async stateStore =>
        {
            var catlet = await stateStore.For<Catlet>().GetByIdAsync(OrphanedCatletId);

            catlet.Should().NotBeNull();
            catlet!.Environment.Should().Be(EryphConstants.DefaultEnvironmentName);
            // Pinned from the host reporting it, not from its environment.
            catlet.SiteId.Should().Be(EryphConstants.DefaultSiteId);
        });
    }

    private async Task Handle(
        string? environment,
        string vmName = "test-catlet",
        Guid? vmId = null,
        Guid? metadataId = null)
    {
        await using var scope = CreateScope();
        var handler = CreateHandler(scope);

        await handler.Handle(new UpdateVMHostInventoryCommand
        {
            HostInventory = new VMHostMachineData { Name = HostName },
            VMInventory =
            [
                new VirtualMachineData
                {
                    VmId = vmId ?? VmId,
                    MetadataId = metadataId ?? MetadataId,
                    Name = vmName,
                    DataStore = EryphConstants.DefaultDataStoreName,
                    Environment = environment,
                    ProjectName = EryphConstants.DefaultProjectName,
                },
            ],
            Timestamp = DateTimeOffset.UtcNow,
        });

        await scope.GetInstance<IStateStore>().SaveChangesAsync();
    }

    private UpdateVMHostInventoryCommandHandler CreateHandler(Scope scope)
    {
        var stateStore = scope.GetInstance<IStateStore>();
        var metadataService = new CatletMetadataService(
            scope.GetInstance<IStateStoreRepository<CatletMetadata>>());

        return new UpdateVMHostInventoryCommandHandler(
            new StubLockManager(),
            metadataService,
            new Mock<IOperationDispatcher>().Object,
            new Mock<IMessageContext>().Object,
            new CatletDataService(
                scope.GetInstance<IStateStoreRepository<Catlet>>(), metadataService, stateStore),
            new VMHostMachineDataService(scope.GetInstance<IStateStoreRepository<CatletFarm>>()),
            _registry,
            stateStore,
            NullLogger.Instance);
    }

    private async Task WithScope(Func<IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        await func(scope.GetInstance<IStateStore>());
    }

    [Fact]
    public async Task The_hosts_site_follows_its_registration()
    {
        // A host is wherever the operator assigned it, so this is read on every round rather than
        // pinned. A host inventoried before it registered would otherwise keep the default site
        // forever, and everything first recorded on it would be pinned to the wrong place.
        await Handle(environment: "staging");

        _registry.SiteId = OtherSiteId;
        await Handle(environment: "staging");

        await WithScope(async stateStore =>
        {
            var farms = await stateStore.For<CatletFarm>().ListAsync();

            farms.Should().ContainSingle().Which.SiteId.Should().Be(OtherSiteId);
        });
    }

    /// <summary>
    /// The host's registration. Reports no agent until a site is assigned, which is the host
    /// inventoried before it registered.
    /// </summary>
    private sealed class StubRegistry : IComponentRegistry
    {
        public Guid? SiteId { get; set; }

        public Seq<HostAgentComponent> GetHostAgents() =>
            SiteId is null
                ? LanguageExt.Seq<HostAgentComponent>.Empty
                : Seq1(new HostAgentComponent(
                    HostName, SiteId.Value, EryphConstants.Networking.LocalChassisName, 1));
    }

    /// <summary>Hand-written: the interface is internal, which Moq cannot proxy.</summary>
    private sealed class StubLockManager : IInventoryLockManager
    {
        public ValueTask AcquireVhdLock(Guid diskIdentifier) => ValueTask.CompletedTask;

        public ValueTask AcquireVmLock(Guid vmId) => ValueTask.CompletedTask;
    }
}
