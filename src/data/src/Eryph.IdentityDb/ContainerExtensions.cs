using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.IdentityDb;

public static class ContainerExtensions
{
    /// <summary>
    /// Registers the in-memory identity store (tests) using the given database root. Mirrors
    /// <c>RegisterInMemoryStateStore</c>.
    /// </summary>
    public static SimpleInjectorAddOptions RegisterInMemoryIdentityStore(
        this SimpleInjectorAddOptions options,
        Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot databaseRoot) =>
        options.RegisterIdentityStore<InMemoryIdentityDbContext>(
            new InMemoryIdentityDbContextConfigurer(databaseRoot));

    /// <summary>
    /// Wires the identity <see cref="IdentityDbContext"/> to the given derived provider context and
    /// registers <paramref name="configurer"/> as the store's <see cref="IDbContextConfigurer{IdentityDbContext}"/>.
    /// <para>
    /// The configurer is registered on the module's SimpleInjector <see cref="Container"/> — the same
    /// container the change-tracking pipeline decorates it on — and the DbContext factory resolves it from
    /// that captured container. The state store can fish the container out of the request
    /// <c>IServiceProvider</c> because its context is only ever resolved inside the module's own scope; the
    /// identity context is also resolved by OpenIddict's Entity Framework stores, which run in a different
    /// container (the split runtime and the web test host), so we bind to the module container explicitly.
    /// <see cref="Container.GetInstance{T}()"/> still resolves against the ambient async scope, so the
    /// scoped change-tracking configurer decorator keeps its correct per-scope lifestyle.
    /// </para>
    /// </summary>
    public static SimpleInjectorAddOptions RegisterIdentityStore<TContext>(
        this SimpleInjectorAddOptions options,
        IDbContextConfigurer<IdentityDbContext> configurer)
        where TContext : IdentityDbContext
    {
        var container = options.Container;
        container.RegisterInstance(configurer);
        options.Services.AddDbContext<IdentityDbContext, TContext>(
            (_, dbOptions) =>
            {
                container.GetInstance<IDbContextConfigurer<IdentityDbContext>>().Configure(dbOptions);
                // OpenIddict's tables are contributed through this options extension; the design-time
                // migration factory applies the same call so generated migrations include them.
                IdentityDbModel.ApplyOpenIddict(dbOptions);
            });

        return options;
    }
}
