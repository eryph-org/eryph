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
/// Covers the system-client reconciliation the seeder performs on top of add-only seeding: when the
/// eryph-zero system client generator regenerates the on-disk certificate, the persisted database row
/// must be updated to match, otherwise the client's assertions are validated against a stale
/// certificate and every request fails with 401. Regular clients stay add-only.
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
    public async Task System_client_certificate_is_reconciled_when_the_stored_one_differs()
    {
        WriteClientFile(EryphConstants.SystemClientId, "new-cert");

        var stored = SystemClientDescriptor("old-cert");
        ClientApplicationDescriptor? updated = null;
        var service = new Mock<IClientService>();
        service
            .Setup(s => s.Get(EryphConstants.SystemClientId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);
        service
            .Setup(s => s.Update(It.IsAny<ClientApplicationDescriptor>(), It.IsAny<CancellationToken>()))
            .Callback<ClientApplicationDescriptor, CancellationToken>((d, _) => updated = d)
            .ReturnsAsync((ClientApplicationDescriptor d, CancellationToken _) => d);

        var seeder = new ClientSeeder(_config, _fileSystem, service.Object);
        await seeder.Execute(CancellationToken.None);

        updated.Should().NotBeNull("the stale system-client certificate must be reconciled");
        updated!.ClientId.Should().Be(EryphConstants.SystemClientId);
        updated.Certificate.Should().Be("new-cert", "the database must follow the on-disk certificate");
        updated.Scopes.Should().BeEquivalentTo(new[] { "compute:write", "identity:write" },
            "the reconciled row must carry the scopes from the file");
        updated.AppRoles.Should().Contain(EryphConstants.SuperAdminRole,
            "the reconciled row must carry the roles from the file");
        service.Verify(s => s.Add(It.IsAny<ClientApplicationDescriptor>(), It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task System_client_is_not_reconciled_when_the_on_disk_certificate_is_missing()
    {
        // A torn/partial write must not wipe the server's validation key.
        WriteClientFile(EryphConstants.SystemClientId, certificate: "");

        var service = new Mock<IClientService>();
        service
            .Setup(s => s.Get(EryphConstants.SystemClientId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SystemClientDescriptor("stored-cert"));

        var seeder = new ClientSeeder(_config, _fileSystem, service.Object);
        await seeder.Execute(CancellationToken.None);

        service.Verify(s => s.Update(It.IsAny<ClientApplicationDescriptor>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task System_client_is_not_touched_when_the_certificate_matches()
    {
        WriteClientFile(EryphConstants.SystemClientId, "same-cert");

        var service = new Mock<IClientService>();
        service
            .Setup(s => s.Get(EryphConstants.SystemClientId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SystemClientDescriptor("same-cert"));

        var seeder = new ClientSeeder(_config, _fileSystem, service.Object);
        await seeder.Execute(CancellationToken.None);

        service.Verify(s => s.Update(It.IsAny<ClientApplicationDescriptor>(),
            It.IsAny<CancellationToken>()), Times.Never);
        service.Verify(s => s.Add(It.IsAny<ClientApplicationDescriptor>(), It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Existing_regular_client_is_left_untouched_even_when_its_certificate_differs()
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

    private static ClientApplicationDescriptor SystemClientDescriptor(string certificate) => new()
    {
        ClientId = EryphConstants.SystemClientId,
        TenantId = TenantId,
        Certificate = certificate,
    };

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
