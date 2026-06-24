using System;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Core;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using Xunit.Abstractions;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Catlets;

public abstract class CatletTestBase : InMemoryStateDbTestBase
{
    protected const string OtherProjectName = "other";

    protected static readonly Guid CatletId = Guid.NewGuid();
    protected static readonly Guid CatletMetadataId = Guid.NewGuid();
    protected static readonly Guid OtherClientId = Guid.NewGuid();
    protected static readonly Guid OtherProjectId = Guid.NewGuid();
    protected readonly WebModuleFactory<ComputeApiModule> Factory;

    protected CatletTestBase(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
        Factory = new WebModuleFactory<ComputeApiModule>()
            .WithApiHost(ConfigureDatabase, RegisterStateStore);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        await stateStore.For<Project>().AddAsync(new Project
        {
            Id = OtherProjectId,
            TenantId = EryphConstants.DefaultTenantId,
            Name = OtherProjectName,
        });

        var metadata = await stateStore.For<CatletMetadata>().AddAsync(new CatletMetadata
        {
            Id = CatletMetadataId,
            Metadata = new CatletMetadataContent(),
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
    }

    protected async Task ArrangeOtherUserAccess(BuiltinRole role, Guid projectId)
    {
        await WithScope(async stateStore =>
        {
            await stateStore.For<ProjectRoleAssignment>().AddAsync(
                new ProjectRoleAssignment
                {
                    ProjectId = projectId,
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

    public override async Task DisposeAsync()
    {
        Factory.Dispose();
        await base.DisposeAsync();
    }
}
