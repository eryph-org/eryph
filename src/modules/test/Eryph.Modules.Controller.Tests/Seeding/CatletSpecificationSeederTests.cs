using Eryph.ConfigModel.Json;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.Seeding;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlCatletSpecificationSeederTests(
    MySqlFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : CatletSpecificationSeederTests(databaseFixture, outputHelper);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteCatletSpecificationSeederTests(
    SqliteFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : CatletSpecificationSeederTests(databaseFixture, outputHelper);

public abstract class CatletSpecificationSeederTests(
    IDatabaseFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : SeederTestBase(databaseFixture, outputHelper)
{
    [Fact]
    public async Task Specification_and_version_are_seeded()
    {
        var specificationId = CatletSpecificationConfigModelTestData.Specification.Id;
        await MockFileSystem.File.WriteAllTextAsync(
            Path.Combine(ChangeTrackingConfig.CatletSpecificationsConfigPath, $"{specificationId}.json"),
            CatletSpecificationConfigModelTestData.SpecificationJson);
        
        var specificationVersionId = CatletSpecificationConfigModelTestData.SpecificationVersion.Id;
        await MockFileSystem.File.WriteAllTextAsync(
            Path.Combine(ChangeTrackingConfig.CatletSpecificationVersionsConfigPath, $"{specificationVersionId}.json"),
            CatletSpecificationConfigModelTestData.SpecificationVersionJson);

        await ExecuteSeeder();

        await WithScope(async stateStore =>
        {
            var specification = await stateStore.Read<CatletSpecification>().GetByIdAsync(specificationId);
            specification.Should().NotBeNull();
            specification!.Id.Should().Be(specificationId);
            specification.ProjectId.Should().Be(CatletSpecificationConfigModelTestData.Specification.ProjectId);
            specification.Name.Should().Be(CatletSpecificationConfigModelTestData.Specification.Name);
            specification.Architecture.Should().Be(CatletSpecificationConfigModelTestData.Specification.Architecture);

            var version = await stateStore.Read<CatletSpecificationVersion>().GetByIdAsync(specificationVersionId);
            version.Should().NotBeNull();
            version!.Id.Should().Be(specificationVersionId);
            version.SpecificationId.Should().Be(specificationId);
            version.ConfigYaml.Should().Be(CatletSpecificationConfigModelTestData.SpecificationVersion.ConfigYaml);
            version.ResolvedConfig.Should().Be(CatletConfigJsonSerializer.Serialize(CatletSpecificationConfigModelTestData.Config));
            version.Comment.Should().Be(CatletSpecificationConfigModelTestData.SpecificationVersion.Comment);
            version.CreatedAt.Should().Be(CatletSpecificationConfigModelTestData.SpecificationVersion.CreatedAt);
            version.Genes.Should().HaveCount(2);
        });

        var specificationBackupContent = await MockFileSystem.File.ReadAllTextAsync(
            Path.Combine(ChangeTrackingConfig.CatletSpecificationsConfigPath, $"{specificationId}.json.bak"));
        specificationBackupContent.Should().Be(CatletSpecificationConfigModelTestData.SpecificationJson);

        var specificationVersionBackupContent = await MockFileSystem.File.ReadAllTextAsync(
            Path.Combine(ChangeTrackingConfig.CatletSpecificationVersionsConfigPath, $"{specificationVersionId}.json.bak"));
        specificationVersionBackupContent.Should().Be(CatletSpecificationConfigModelTestData.SpecificationVersionJson);

        var updatedSpecificationContent = await MockFileSystem.File.ReadAllTextAsync(
            Path.Combine(ChangeTrackingConfig.CatletSpecificationsConfigPath, $"{specificationId}.json"));
        updatedSpecificationContent.Should().Be(CatletSpecificationConfigModelTestData.SpecificationJson);

        var updatedSpecificationVersionContent = await MockFileSystem.File.ReadAllTextAsync(
            Path.Combine(ChangeTrackingConfig.CatletSpecificationVersionsConfigPath, $"{specificationVersionId}.json"));
        updatedSpecificationVersionContent.Should().Be(CatletSpecificationConfigModelTestData.SpecificationVersionJson);
    }

    private async Task ExecuteSeeder()
    {
        using var host = CreateHost();
        await host.StartAsync();
        await host.StopAsync();
    }

    protected async Task WithScope(Func<IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(stateStore);
    }
}
