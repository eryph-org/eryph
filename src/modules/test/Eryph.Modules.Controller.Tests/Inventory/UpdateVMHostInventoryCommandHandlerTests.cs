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
    public async Task An_unmanaged_vm_with_an_unattributable_path_is_skipped_not_inserted()
    {
        // No metadata for this VM id, and no environment: it must not be inserted with an empty
        // environment, which would be an unusable identity.
        await Handle(environment: null, vmId: Guid.NewGuid(), metadataId: Guid.NewGuid());

        await WithScope(async stateStore =>
        {
            var catlets = await stateStore.For<Catlet>().ListAsync();

            catlets.Should().ContainSingle().Which.Id.Should().Be(CatletId);
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
            new StubRegistry(),
            stateStore,
            NullLogger.Instance);
    }

    private async Task WithScope(Func<IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        await func(scope.GetInstance<IStateStore>());
    }

    /// <summary>The host has not registered, so it falls back to the default site.</summary>
    private sealed class StubRegistry : IComponentRegistry
    {
        public Seq<HostAgentComponent> GetHostAgents() => Seq<HostAgentComponent>.Empty;
    }

    /// <summary>Hand-written: the interface is internal, which Moq cannot proxy.</summary>
    private sealed class StubLockManager : IInventoryLockManager
    {
        public ValueTask AcquireVhdLock(Guid diskIdentifier) => ValueTask.CompletedTask;

        public ValueTask AcquireVmLock(Guid vmId) => ValueTask.CompletedTask;
    }
}
