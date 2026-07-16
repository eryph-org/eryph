using Eryph.Core;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Eryph.StateDb.TestBase;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Eryph.StateDb.Tests;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlCatletIdentityTests(
    MySqlFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : CatletIdentityTests(databaseFixture, outputHelper);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteCatletIdentityTests(
    SqliteFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : CatletIdentityTests(databaseFixture, outputHelper);

/// <summary>
/// A catlet is identified by its name within a project AND environment, and a specification deploys
/// into many environments but at most once into each. Both are enforced by the database, not only by
/// the endpoints which check them first: they are what the environment means.
/// </summary>
public abstract class CatletIdentityTests(
    IDatabaseFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : StateDbTestBase(databaseFixture, outputHelper)
{
    private static readonly Guid SpecificationId = new("b7c8d9e0-1111-4222-8333-444455556666");

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
    }

    [Fact]
    public async Task The_same_name_can_exist_in_different_environments_of_a_project()
    {
        await Add(name: "web", environment: "dev");
        await Add(name: "web", environment: "test");

        await WithScope(async stateStore =>
        {
            var catlets = await stateStore.For<Catlet>().ListAsync(
                new ResourceSpecs<Catlet>.GetByName("web"));

            catlets.Should().HaveCount(2);
        });
    }

    [Fact]
    public async Task The_same_name_cannot_be_used_twice_in_one_environment()
    {
        await Add(name: "web", environment: "dev");

        var act = () => Add(name: "web", environment: "dev");

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task A_specification_can_be_deployed_into_several_environments()
    {
        await Add(name: "web-dev", environment: "dev", specificationId: SpecificationId);
        await Add(name: "web-test", environment: "test", specificationId: SpecificationId);

        await WithScope(async stateStore =>
        {
            var catlets = await stateStore.For<Catlet>().ListAsync(
                new CatletSpecs.ListBySpecificationId(SpecificationId));

            catlets.Should().HaveCount(2);
        });
    }

    [Fact]
    public async Task A_specification_cannot_be_deployed_twice_into_one_environment()
    {
        await Add(name: "web", environment: "dev", specificationId: SpecificationId);

        var act = () => Add(name: "web-again", environment: "dev", specificationId: SpecificationId);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Deployments_of_several_specifications_are_listed_in_one_query()
    {
        // Listing specifications resolves every deployment with this one query instead of one per
        // specification. It filters a nullable key, which the provider has to translate — a thing
        // that only fails when it actually runs against a database.
        var otherSpecificationId = new Guid("c8d9e0f1-2222-4333-8444-555566667777");
        await Add(name: "web-dev", environment: "dev", specificationId: SpecificationId);
        await Add(name: "web-test", environment: "test", specificationId: SpecificationId);
        await Add(name: "api-dev", environment: "dev", specificationId: otherSpecificationId);
        // Not deployed from a specification: must not be picked up by the key filter.
        await Add(name: "loose", environment: "dev");

        await WithScope(async stateStore =>
        {
            var catlets = await stateStore.For<Catlet>().ListAsync(
                new CatletSpecs.ListBySpecificationIds([SpecificationId, otherSpecificationId]));

            catlets.Select(c => c.Name).Should()
                .BeEquivalentTo(["web-dev", "web-test", "api-dev"]);
        });
    }

    [Fact]
    public async Task Listing_deployments_of_no_specifications_returns_nothing()
    {
        await Add(name: "web-dev", environment: "dev", specificationId: SpecificationId);

        await WithScope(async stateStore =>
        {
            var catlets = await stateStore.For<Catlet>().ListAsync(
                new CatletSpecs.ListBySpecificationIds([]));

            catlets.Should().BeEmpty();
        });
    }

    [Fact]
    public async Task A_deployment_is_found_by_specification_and_environment()
    {
        await Add(name: "web-dev", environment: "dev", specificationId: SpecificationId);
        await Add(name: "web-test", environment: "test", specificationId: SpecificationId);

        await WithScope(async stateStore =>
        {
            var catlet = await stateStore.For<Catlet>().GetBySpecAsync(
                new CatletSpecs.GetBySpecificationIdAndEnvironment(SpecificationId, "test"));

            catlet!.Name.Should().Be("web-test");
        });
    }

    [Fact]
    public async Task Undeployed_catlets_do_not_collide_on_the_specification_index()
    {
        // Both have no specification: the (SpecificationId, Environment) index must not treat two
        // nulls in one environment as a duplicate.
        await Add(name: "one", environment: "dev");
        await Add(name: "two", environment: "dev");

        await WithScope(async stateStore =>
        {
            var catlets = await stateStore.For<Catlet>().ListAsync();

            catlets.Should().HaveCount(2);
        });
    }

    private async Task Add(string name, string environment, Guid? specificationId = null)
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();
        await stateStore.For<Catlet>().AddAsync(new Catlet
        {
            Id = Guid.NewGuid(),
            ProjectId = EryphConstants.DefaultProjectId,
            SiteId = EryphConstants.DefaultSiteId,
            Name = name,
            Environment = environment,
            DataStore = EryphConstants.DefaultDataStoreName,
            SpecificationId = specificationId,
        });
        await stateStore.SaveChangesAsync();
    }

    private async Task WithScope(Func<IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        await func(scope.GetInstance<IStateStore>());
    }
}
