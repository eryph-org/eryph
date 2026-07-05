using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.Identity.Bootstrap;
using Eryph.Modules.Identity.Services;
using Eryph.Modules.Identity.Test.Services;
using Eryph.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Eryph.Modules.Identity.Test.Bootstrap;

/// <summary>
/// Covers the idempotent reconciliation <see cref="SystemClientBootstrap"/> performs between the
/// on-disk private key and the identity-database client row: it creates the system-client when absent,
/// leaves a consistent one untouched, and re-establishes the database row against the existing key
/// (never rotating the break-glass credential) when the row drifts or is lost.
/// </summary>
public class SystemClientBootstrapTests
{
    private static readonly string[] RequiredScopes =
    [
        EryphConstants.Authorization.Scopes.ComputeWrite,
        EryphConstants.Authorization.Scopes.IdentityWrite,
    ];

    [Fact]
    public async Task First_boot_generates_the_key_and_creates_the_client()
    {
        var keyStore = new FakeKeyStore();
        var clients = new ClientStore();
        var bootstrap = NewBootstrap(keyStore, clients.Service);

        await bootstrap.ExecuteAsync(CancellationToken.None);

        keyStore.Writes.Should().Be(1, "a new break-glass key must be generated and persisted on first boot");
        clients.Added.Should().Be(1);
        clients.Updated.Should().Be(0);

        var row = clients.Get(EryphConstants.SystemClientId);
        row.Should().NotBeNull();
        row!.TenantId.Should().Be(EryphConstants.DefaultTenantId);
        row.Scopes.Should().BeEquivalentTo(RequiredScopes);
        row.AppRoles.Should().Contain(EryphConstants.SuperAdminRole);
        row.ClientSecret.Should().BeNull("the system client authenticates with its certificate, not a secret");
        CertificateMatchesKey(row.Certificate, keyStore.CurrentKey()).Should().BeTrue();
    }

    [Fact]
    public async Task A_consistent_system_client_is_left_untouched()
    {
        var (keyStore, _) = await Bootstrapped();

        // Second run against the already-created row and the same key must do nothing.
        var clients = new ClientStore();
        clients.Seed(await CreateRow(keyStore));
        var bootstrap = NewBootstrap(keyStore, clients.Service);

        await bootstrap.ExecuteAsync(CancellationToken.None);

        keyStore.Writes.Should().Be(1, "an existing key must never be rotated");
        clients.Added.Should().Be(0);
        clients.Updated.Should().Be(0);
    }

    [Fact]
    public async Task A_missing_row_is_recreated_from_the_existing_key_without_rotating_it()
    {
        // The eryph-zero identity database is disposable: after a reset the key is still on disk but the
        // row is gone. The bootstrap must re-register the client from the SAME key.
        var (keyStore, originalRow) = await Bootstrapped();
        var clients = new ClientStore(); // empty — simulates the reset database

        var bootstrap = NewBootstrap(keyStore, clients.Service);
        await bootstrap.ExecuteAsync(CancellationToken.None);

        keyStore.Writes.Should().Be(1, "the existing break-glass key must be reused, not rotated");
        clients.Added.Should().Be(1);
        var row = clients.Get(EryphConstants.SystemClientId);
        CertificateMatchesKey(row!.Certificate, keyStore.CurrentKey()).Should().BeTrue();
        CertificateMatchesKey(originalRow.Certificate, keyStore.CurrentKey()).Should().BeTrue();
    }

    [Fact]
    public async Task A_drifted_certificate_is_reconciled_against_the_key_without_rotating_it()
    {
        var (keyStore, _) = await Bootstrapped();
        var clients = new ClientStore();
        // A row whose certificate belongs to a different key (e.g. the key was restored from backup):
        // assertions signed with the on-disk key would fail against it.
        clients.Seed(await CreateRow(new FakeKeyStore()));

        var bootstrap = NewBootstrap(keyStore, clients.Service);
        await bootstrap.ExecuteAsync(CancellationToken.None);

        keyStore.Writes.Should().Be(1, "reconciling a drifted certificate must not rotate the key");
        clients.Added.Should().Be(0);
        clients.Updated.Should().Be(1);
        var row = clients.Get(EryphConstants.SystemClientId);
        CertificateMatchesKey(row!.Certificate, keyStore.CurrentKey()).Should().BeTrue();
    }

    [Fact]
    public async Task Drifted_scopes_are_reapplied()
    {
        var (keyStore, _) = await Bootstrapped();
        var clients = new ClientStore();
        var row = await CreateRow(keyStore); // certificate matches the key, but the scopes are wrong
        row.Scopes.Clear();
        row.Scopes.Add(EryphConstants.Authorization.Scopes.ComputeWrite);
        clients.Seed(row);

        var bootstrap = NewBootstrap(keyStore, clients.Service);
        await bootstrap.ExecuteAsync(CancellationToken.None);

        keyStore.Writes.Should().Be(1);
        clients.Updated.Should().Be(1);
        clients.Get(EryphConstants.SystemClientId)!.Scopes.Should().BeEquivalentTo(RequiredScopes);
    }

    private static SystemClientBootstrap NewBootstrap(ISystemClientKeyStore keyStore, IClientService clientService) =>
        new(keyStore, clientService, new CertificateGenerator(), new InMemoryKeyService(),
            NullLogger<SystemClientBootstrap>.Instance);

    // Runs the bootstrap once from a clean slate and returns the resulting key store + created row.
    private static async Task<(FakeKeyStore keyStore, ClientApplicationDescriptor row)> Bootstrapped()
    {
        var keyStore = new FakeKeyStore();
        var clients = new ClientStore();
        await NewBootstrap(keyStore, clients.Service).ExecuteAsync(CancellationToken.None);
        return (keyStore, clients.Get(EryphConstants.SystemClientId)!);
    }

    // Produces a valid system-client row for the given key store's key by running the bootstrap against
    // an empty store, so the certificate is built exactly as production does.
    private static async Task<ClientApplicationDescriptor> CreateRow(FakeKeyStore keyStore)
    {
        var clients = new ClientStore();
        await NewBootstrap(keyStore, clients.Service).ExecuteAsync(CancellationToken.None);
        return clients.Get(EryphConstants.SystemClientId)!;
    }

    private static bool CertificateMatchesKey(string? certificateBase64, RSA? key)
    {
        if (string.IsNullOrEmpty(certificateBase64) || key is null)
            return false;

        using var certificate =
            X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certificateBase64));
        using var publicKey = certificate.GetRSAPublicKey();
        return publicKey is not null
               && key.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(publicKey.ExportSubjectPublicKeyInfo());
    }

    /// <summary>In-memory <see cref="ISystemClientKeyStore"/> holding the key as PEM (fresh instance per read).</summary>
    private sealed class FakeKeyStore : ISystemClientKeyStore
    {
        private string? _pem;

        public int Writes { get; private set; }

        public Task<RSA?> TryReadKey(CancellationToken cancellationToken) =>
            Task.FromResult(FromPem(_pem));

        public Task WriteKey(RSA key, CancellationToken cancellationToken)
        {
            _pem = key.ExportRSAPrivateKeyPem();
            Writes++;
            return Task.CompletedTask;
        }

        public RSA? CurrentKey() => FromPem(_pem);

        private static RSA? FromPem(string? pem)
        {
            if (pem is null)
                return null;
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return rsa;
        }
    }

    /// <summary>A mock <see cref="IClientService"/> backed by an in-memory dictionary, tracking calls.</summary>
    private sealed class ClientStore
    {
        private readonly Dictionary<string, ClientApplicationDescriptor> _clients = new();

        public ClientStore()
        {
            var service = new Mock<IClientService>();
            service.Setup(s => s.Get(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, Guid _, CancellationToken _) =>
                    _clients.TryGetValue(id, out var d) ? d : null);
            service.Setup(s => s.Add(It.IsAny<ClientApplicationDescriptor>(), It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ClientApplicationDescriptor, bool, CancellationToken>((d, _, _) =>
                {
                    _clients[d.ClientId!] = d;
                    Added++;
                })
                .ReturnsAsync((ClientApplicationDescriptor d, bool _, CancellationToken _) => d);
            service.Setup(s => s.Update(It.IsAny<ClientApplicationDescriptor>(), It.IsAny<CancellationToken>()))
                .Callback<ClientApplicationDescriptor, CancellationToken>((d, _) =>
                {
                    _clients[d.ClientId!] = d;
                    Updated++;
                })
                .ReturnsAsync((ClientApplicationDescriptor d, CancellationToken _) => d);
            Service = service.Object;
        }

        public IClientService Service { get; }

        public int Added { get; private set; }

        public int Updated { get; private set; }

        public void Seed(ClientApplicationDescriptor descriptor) => _clients[descriptor.ClientId!] = descriptor;

        public ClientApplicationDescriptor? Get(string clientId) =>
            _clients.TryGetValue(clientId, out var d) ? d : null;
    }
}
