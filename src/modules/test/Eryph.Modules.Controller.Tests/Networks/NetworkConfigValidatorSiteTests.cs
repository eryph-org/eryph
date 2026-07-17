using Eryph.ConfigModel;
using Eryph.ConfigModel.Networks;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.TestBase;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.Networks;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlNetworkConfigValidatorSiteTests(
    ITestOutputHelper outputHelper, MySqlFixture databaseFixture)
    : NetworkConfigValidatorSiteTests(outputHelper, databaseFixture);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteNetworkConfigValidatorSiteTests(
    ITestOutputHelper outputHelper, SqliteFixture databaseFixture)
    : NetworkConfigValidatorSiteTests(outputHelper, databaseFixture);

/// <summary>
/// The networks of a project are connected by one router, which exists in a single site, so the
/// environments a project declares networks for must all be realized by the same site.
/// </summary>
public abstract class NetworkConfigValidatorSiteTests(
    ITestOutputHelper outputHelper, IDatabaseFixture databaseFixture)
    : StateDbTestBase(databaseFixture, outputHelper)
{
    private static readonly Guid ElsewhereSiteId = new("c1c2c3c4-0000-4000-8000-000000000001");

    private static readonly NetworkProvider[] Providers =
    [
        new() { Name = "default", Type = NetworkProviderType.NatOverlay },
    ];

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
    }

    [Fact]
    public async Task Networks_of_environments_in_one_site_are_accepted()
    {
        var errors = await Validate(
            new SiteAwareSiteResolver(("staging", EryphConstants.DefaultSiteId)),
            Config("default", "staging"));

        errors.Should().NotContain(e => e.Contains("different sites"));
    }

    [Fact]
    public async Task Networks_of_environments_in_different_sites_are_refused()
    {
        var errors = await Validate(
            new SiteAwareSiteResolver(("staging", ElsewhereSiteId)),
            Config("default", "staging"));

        errors.Should().ContainSingle(e => e.Contains("different sites"))
            .Which.Should().Contain("one router");
    }

    [Fact]
    public async Task An_unknown_environment_is_reported()
    {
        var errors = await Validate(new FailingSiteResolver(), Config("staging"));

        errors.Should().ContainSingle(e => e.Contains("staging"));
    }

    private static ProjectNetworksConfig Config(params string[] environments) =>
        new()
        {
            Project = "default",
            Networks = environments.Select(e => new NetworkConfig
            {
                Name = "default",
                Environment = e,
                Address = "192.168.10.0/24",
                Provider = new ProviderConfig { Name = "default" },
            }).ToArray(),
        };

    private async Task<string[]> Validate(ISiteResolver siteResolver, ProjectNetworksConfig config)
    {
        await using var scope = CreateScope();
        var validator = new NetworkConfigValidator(
            scope.GetInstance<IStateStore>(), siteResolver, NullLogger.Instance);

        var errors = await validator
            .ValidateChanges(EryphConstants.DefaultProjectId, config, Providers)
            .ToListAsync();

        foreach (var error in errors)
            outputHelper.WriteLine(error);

        return errors.ToArray();
    }

    private sealed class FailingSiteResolver : ISiteResolver
    {
        public Task<Either<Error, Guid>> ResolveSite(
            EnvironmentName environment,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(LanguageExt.Prelude.Left<Error, Guid>(
                Error.New($"The environment '{environment}' is not part of the environment configuration.")));
    }
}
