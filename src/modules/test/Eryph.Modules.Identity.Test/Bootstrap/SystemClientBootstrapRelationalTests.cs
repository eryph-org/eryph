using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.IdentityDb;
using Eryph.IdentityDb.Entities;
using Eryph.Modules.Identity.Bootstrap;
using Eryph.Modules.Identity.Services;
using Eryph.Modules.Identity.Test.Services;
using Eryph.Security.Cryptography;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using Xunit;

namespace Eryph.Modules.Identity.Test.Bootstrap;

/// <summary>
/// Exercises <see cref="SystemClientBootstrap"/> and <see cref="IClientService"/> against a
/// <em>relational</em> (SQLite) identity store with the real OpenIddict application manager — the path
/// every prior test skipped by using the in-memory provider (which ignores table names). This is where
/// the standalone-identity blocker actually lived: the OpenIddict tables map to <c>OpenIddictApplications</c>
/// etc., so the schema must be created with <see cref="IdentityDbModel.ApplyOpenIddict"/> or the first
/// client query fails against a missing table.
/// </summary>
public class SystemClientBootstrapRelationalTests
{
    [Fact]
    public async Task Bootstrap_creates_the_system_client_on_a_relational_store()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var provider = BuildProvider(connection, applyOpenIddict: true);
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
                .Database.EnsureCreatedAsync();
        }

        var keyStore = new InMemoryKeyStore();

        await using (var scope = provider.CreateAsyncScope())
        {
            await NewBootstrap(scope.ServiceProvider, keyStore).ExecuteAsync(CancellationToken.None);
        }

        // A fresh scope proves the row was actually committed to the relational store.
        await using (var scope = provider.CreateAsyncScope())
        {
            var clientService = NewClientService(scope.ServiceProvider);
            var row = await clientService.Get(
                EryphConstants.SystemClientId, EryphConstants.DefaultTenantId, CancellationToken.None);

            row.Should().NotBeNull("the bootstrap must persist the system client to the relational store");
            row!.Scopes.Should().BeEquivalentTo(new[]
            {
                EryphConstants.Authorization.Scopes.ComputeWrite,
                EryphConstants.Authorization.Scopes.IdentityWrite,
                EryphConstants.Authorization.Scopes.ManagementWrite,
            });
            row.AppRoles.Should().Contain(EryphConstants.SuperAdminRole);
            CertificateMatchesKey(row.Certificate, keyStore.Peek()).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Bootstrap_is_idempotent_on_a_relational_store()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var provider = BuildProvider(connection, applyOpenIddict: true);
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
                .Database.EnsureCreatedAsync();
        }

        var keyStore = new InMemoryKeyStore();

        for (var run = 0; run < 2; run++)
            await using (var scope = provider.CreateAsyncScope())
            {
                await NewBootstrap(scope.ServiceProvider, keyStore).ExecuteAsync(CancellationToken.None);
            }

        keyStore.Writes.Should().Be(1, "the second run must reuse the key and not rotate it");

        await using (var scope = provider.CreateAsyncScope())
        {
            var clients = await NewClientService(scope.ServiceProvider)
                .List(EryphConstants.DefaultTenantId, CancellationToken.None);
            clients.Should().ContainSingle(c => c.ClientId == EryphConstants.SystemClientId);
        }
    }

    [Fact]
    public async Task Bootstrap_reconciles_a_drifted_certificate_on_a_relational_store()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var provider = BuildProvider(connection, applyOpenIddict: true);
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
                .Database.EnsureCreatedAsync();
        }

        var keyStore = new InMemoryKeyStore();
        await using (var scope = provider.CreateAsyncScope())
        {
            await NewBootstrap(scope.ServiceProvider, keyStore).ExecuteAsync(CancellationToken.None);
        }

        // Drift the stored certificate to one belonging to a different key (as if the key were restored
        // from backup): assertions signed with the on-disk key would now fail against the stored row.
        using (var otherKey = RSA.Create(2048))
        await using (var scope = provider.CreateAsyncScope())
        {
            var service = NewClientService(scope.ServiceProvider);
            var row = await service.Get(
                EryphConstants.SystemClientId, EryphConstants.DefaultTenantId, CancellationToken.None);
            row!.Certificate = SelfSignedCertificateBase64(otherKey);
            await service.Update(row, CancellationToken.None);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            await NewBootstrap(scope.ServiceProvider, keyStore).ExecuteAsync(CancellationToken.None);
        }

        keyStore.Writes.Should().Be(1, "reconciling a drifted certificate must not rotate the key");
        await using (var scope = provider.CreateAsyncScope())
        {
            var row = await NewClientService(scope.ServiceProvider).Get(
                EryphConstants.SystemClientId, EryphConstants.DefaultTenantId, CancellationToken.None);
            CertificateMatchesKey(row!.Certificate, keyStore.Peek()).Should()
                .BeTrue("the drifted certificate must be rebuilt from the on-disk key through the real store");
        }
    }

    [Fact]
    public async Task EnsureCreated_builds_the_OpenIddict_tables_only_with_ApplyOpenIddict()
    {
        // Guards the create-db fix: without ApplyOpenIddict the runtime table (OpenIddictApplications)
        // is never created, which is exactly the missing-table failure the standalone host hit.
        (await OpenIddictApplicationsTableExists(applyOpenIddict: true)).Should().BeTrue();
        (await OpenIddictApplicationsTableExists(applyOpenIddict: false)).Should().BeFalse();
    }

    private static async Task<bool> OpenIddictApplicationsTableExists(bool applyOpenIddict)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var builder = new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection);
        if (applyOpenIddict)
            IdentityDbModel.ApplyOpenIddict(builder);

        await using (var context = new IdentityDbContext(builder.Options))
        {
            await context.Database.EnsureCreatedAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='OpenIddictApplications';";
        return Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
    }

    private static ServiceProvider BuildProvider(SqliteConnection connection, bool applyOpenIddict)
    {
        var services = new ServiceCollection();
        services.AddDbContext<IdentityDbContext>((_, options) =>
        {
            options.UseSqlite(connection);
            if (applyOpenIddict)
                IdentityDbModel.ApplyOpenIddict(options);
        });
        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore()
                .UseDbContext<IdentityDbContext>()
                .ReplaceDefaultEntities<ApplicationEntity, AuthorizationEntity,
                    OpenIddictEntityFrameworkCoreScope, TokenEntity, string>());

        return services.BuildServiceProvider();
    }

    private static IClientService NewClientService(IServiceProvider scopeServices) =>
        new ClientService(
            scopeServices.GetRequiredService<IOpenIddictApplicationManager>(),
            new IdentityDbRepository<ClientApplicationEntity>(
                scopeServices.GetRequiredService<IdentityDbContext>()));

    private static SystemClientBootstrap NewBootstrap(IServiceProvider scopeServices, ISystemClientKeyStore keyStore) =>
        new(keyStore, NewClientService(scopeServices), new CertificateGenerator(), new InMemoryKeyService(),
            NullLogger<SystemClientBootstrap>.Instance);

    private static string SelfSignedCertificateBase64(RSA key)
    {
        var subject = new X500DistinguishedNameBuilder();
        subject.AddCommonName("drift");
        using var certificate = new CertificateGenerator().GenerateSelfSignedCertificate(
            subject.Build(), "drift", key, 30,
            [new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true)]);
        return Convert.ToBase64String(certificate.Export(X509ContentType.Cert));
    }

    private static bool CertificateMatchesKey(string? certificateBase64, RSA? key)
    {
        // The caller passes a throwaway key freshly created by the fake store, so dispose it here.
        using (key)
        {
            if (string.IsNullOrEmpty(certificateBase64) || key is null)
                return false;

            using var certificate =
                X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certificateBase64));
            using var publicKey = certificate.GetRSAPublicKey();
            return publicKey is not null
                   && key.ExportSubjectPublicKeyInfo().AsSpan()
                       .SequenceEqual(publicKey.ExportSubjectPublicKeyInfo());
        }
    }

    private sealed class InMemoryKeyStore : ISystemClientKeyStore
    {
        private string? _pem;

        public int Writes { get; private set; }

        public Task<RSA?> TryReadKey(CancellationToken cancellationToken) => Task.FromResult(Read());

        public Task WriteKey(RSA key, CancellationToken cancellationToken)
        {
            _pem = key.ExportRSAPrivateKeyPem();
            Writes++;
            return Task.CompletedTask;
        }

        public RSA? Peek() => Read();

        private RSA? Read()
        {
            if (_pem is null)
                return null;
            var rsa = RSA.Create();
            rsa.ImportFromPem(_pem);
            return rsa;
        }
    }
}
