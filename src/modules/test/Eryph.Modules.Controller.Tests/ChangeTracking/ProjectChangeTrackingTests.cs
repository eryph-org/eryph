using System.Text;
using System.Text.Json;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.ChangeTracking;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlProjectChangeTrackingTests(
    MySqlFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : ProjectChangeTrackingTests(databaseFixture, outputHelper);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteProjectChangeTrackingTests(
    SqliteFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : ProjectChangeTrackingTests(databaseFixture, outputHelper);

public abstract class ProjectChangeTrackingTests(
    IDatabaseFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : ChangeTrackingTestBase(databaseFixture, outputHelper)
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid AssignmentId = Guid.NewGuid();
    private const string IdentityId = "test-identity";

    private readonly ProjectConfigModel _expectedConfig = new()
    {
        Name = "test-project",
        TenantId = EryphConstants.DefaultTenantId,
        Assignments =
        [
            new ProjectRoleAssignmentConfigModel()
            {
                IdentityId = IdentityId,
                RoleName = "contributor",
            },
        ],
    };

    [Fact]
    public async Task Project_new_is_detected()
    {
        var newProjectId = Guid.NewGuid();
        await WithHostScope(async stateStore =>
        {
            await stateStore.For<Project>().AddAsync(new Project()
            {
                Id = newProjectId,
                Name = "new-project",
                TenantId = EryphConstants.DefaultTenantId,
            });

            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig(newProjectId);
        config.Should().BeEquivalentTo(new ProjectConfigModel()
        {
            Name = "new-project",
            TenantId = EryphConstants.DefaultTenantId,
            Assignments = [],
        });
    }

    [Fact]
    public async Task Project_update_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var project = await stateStore.For<Project>().GetByIdAsync(ProjectId);
            project!.Name = "new-name";
            project.Deleted = true;

            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig(ProjectId);
        _expectedConfig.Name = "new-name";
        _expectedConfig.Deleted = true;
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task Project_delete_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var project = await stateStore.For<Project>().GetByIdAsync(ProjectId);
            project!.Name = "new-name";

            await stateStore.SaveChangesAsync();
        });
        MockFileSystem.File.Exists(GetConfigPath(ProjectId)).Should().BeTrue();

        await WithHostScope(async stateStore =>
        {
            var project = await stateStore.For<Project>().GetByIdAsync(ProjectId);
            await stateStore.For<Project>().DeleteAsync(project!);

            await stateStore.SaveChangesAsync();
        });

        MockFileSystem.File.Exists(GetConfigPath(ProjectId)).Should().BeFalse();
    }

    [Fact]
    public async Task RoleAssignment_new_is_detected()
    {
        const string secondIdentityId = "second-identity";

        await WithHostScope(async stateStore =>
        {
            var project = await stateStore.For<Project>().GetByIdAsync(ProjectId);
            await stateStore.LoadCollectionAsync(project!, p => p.ProjectRoles);
            project!.ProjectRoles.Add(new ProjectRoleAssignment()
            {
                IdentityId = secondIdentityId,
                RoleId = EryphConstants.BuildInRoles.Reader,
            });

            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig(ProjectId);
        _expectedConfig.Assignments =
        [
            .. _expectedConfig.Assignments,
            new ProjectRoleAssignmentConfigModel()
            {
                IdentityId = secondIdentityId,
                RoleName = "reader",
            },
        ];
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task RoleAssignment_delete_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var assignment = await stateStore.For<ProjectRoleAssignment>().GetByIdAsync(AssignmentId);
            await stateStore.For<ProjectRoleAssignment>().DeleteAsync(assignment!);

            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig(ProjectId);
        _expectedConfig.Assignments = [];
        config.Should().BeEquivalentTo(_expectedConfig);
    }


    private async Task<ProjectConfigModel> ReadConfig(Guid projectId)
    {
        var json = await MockFileSystem.File.ReadAllTextAsync(
            GetConfigPath(projectId), Encoding.UTF8);

        return JsonSerializer.Deserialize<ProjectConfigModel>(json)!;
    }

    private string GetConfigPath(Guid projectId) =>
        Path.Combine(ChangeTrackingConfig.ProjectNetworksConfigPath, $"{projectId}.json");

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        await stateStore.For<Project>().AddAsync(new Project()
        {
            Id = ProjectId,
            Name = "test-project",
            TenantId = EryphConstants.DefaultTenantId,
            ProjectRoles =
            [
                new ProjectRoleAssignment()
                {
                    Id = AssignmentId,
                    IdentityId = "test-identity",
                    RoleId = EryphConstants.BuildInRoles.Contributor,
                },
            ],
        });
    }
}