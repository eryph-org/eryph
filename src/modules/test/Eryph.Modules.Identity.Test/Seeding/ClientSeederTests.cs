using System;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.Modules.Identity.ChangeTracking;
using Eryph.Modules.Identity.Seeding;
using Eryph.Modules.Identity.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Eryph.Modules.Identity.Test.Seeding;

/// <summary>
/// The client seeder rebuilds regular clients from the on-disk mirror (add-only). The system client is
/// not seeded here at all — it is owned end to end by <c>SystemClientBootstrap</c> — so the seeder must
/// skip it entirely, even though a mirror file for it exists (written by change-tracking export).
/// </summary>
public class ClientSeederTests : IDisposable
{
    private static readonly Guid TenantId = EryphConstants.DefaultTenantId;

    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "eryph-id-client-seed-" + Guid.NewGuid().ToString("N"));

    private readonly IFileSystem _fileSystem = new FileSystem();
    private readonly IdentityChangeTrackingConfig _config;

    public ClientSeederTests()
    {
        _config = new IdentityChangeTrackingConfig
        {
            SeedDatabase = true,
            ClientsConfigPath = Path.Combine(_dir, "clients"),
        };
    }

    public void Dispose()
    {
        if (_fileSystem.Directory.Exists(_dir))
            _fileSystem.Directory.Delete(_dir, true);
    }

    [Fact]
    public async Task System_client_is_skipped_entirely()
    {
        WriteClientFile(EryphConstants.SystemClientId, "new-cert");

        var service = new Mock<IClientService>();
        var seeder = new ClientSeeder(_config, _fileSystem, service.Object);
        await seeder.Execute(CancellationToken.None);

        // The bootstrap owns the system client; the seeder must not even query it, let alone add or update.
        service.Verify(s => s.Get(EryphConstants.SystemClientId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        service.Verify(s => s.Add(It.IsAny<ClientApplicationDescriptor>(), It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
        service.Verify(s => s.Update(It.IsAny<ClientApplicationDescriptor>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task New_regular_client_is_added()
    {
        WriteClientFile("regular-client", "file-cert");

        var service = new Mock<IClientService>();
        service
            .Setup(s => s.Get("regular-client", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientApplicationDescriptor?)null);
        ClientApplicationDescriptor? added = null;
        service
            .Setup(s => s.Add(It.IsAny<ClientApplicationDescriptor>(), true, It.IsAny<CancellationToken>()))
            .Callback<ClientApplicationDescriptor, bool, CancellationToken>((d, _, _) => added = d)
            .ReturnsAsync((ClientApplicationDescriptor d, bool _, CancellationToken _) => d);

        var seeder = new ClientSeeder(_config, _fileSystem, service.Object);
        await seeder.Execute(CancellationToken.None);

        added.Should().NotBeNull();
        added!.ClientId.Should().Be("regular-client");
        added.Certificate.Should().Be("file-cert");
    }

    [Fact]
    public async Task Existing_regular_client_is_left_untouched()
    {
        WriteClientFile("regular-client", "file-cert");

        var stored = new ClientApplicationDescriptor
        {
            ClientId = "regular-client",
            TenantId = TenantId,
            Certificate = "stored-cert",
        };
        var service = new Mock<IClientService>();
        service
            .Setup(s => s.Get("regular-client", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        var seeder = new ClientSeeder(_config, _fileSystem, service.Object);
        await seeder.Execute(CancellationToken.None);

        service.Verify(s => s.Update(It.IsAny<ClientApplicationDescriptor>(),
            It.IsAny<CancellationToken>()), Times.Never);
        service.Verify(s => s.Add(It.IsAny<ClientApplicationDescriptor>(), It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private void WriteClientFile(string clientId, string certificate)
    {
        _fileSystem.Directory.CreateDirectory(_config.ClientsConfigPath);
        var model = new ClientConfigModel
        {
            ClientId = clientId,
            TenantId = TenantId,
            X509CertificateBase64 = certificate,
            AllowedScopes = ["compute:write", "identity:write"],
            Roles = [EryphConstants.SuperAdminRole],
        };
        _fileSystem.File.WriteAllText(
            Path.Combine(_config.ClientsConfigPath, $"{clientId}.json"),
            JsonSerializer.Serialize(model));
    }
}
