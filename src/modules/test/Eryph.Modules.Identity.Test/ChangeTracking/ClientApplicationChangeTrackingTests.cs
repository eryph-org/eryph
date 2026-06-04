using System;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.Modules.Identity.ChangeTracking;
using Eryph.Modules.Identity.ChangeTracking.Clients;
using Eryph.Modules.Identity.Seeding;
using Eryph.Modules.Identity.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Eryph.Modules.Identity.Test.ChangeTracking;

/// <summary>
/// Proves the client export/seed round trip that replaced the old write-through decorator: a client is
/// exported to the on-disk mirror in the <see cref="ClientConfigModel"/> format, and the seeder rebuilds
/// it (via <c>IClientService.Add(hashedSecret: true)</c>) after a database drop. Uses a mocked
/// <see cref="IClientService"/> so the round trip is verified without the full OpenIddict stack.
/// </summary>
public class ClientApplicationChangeTrackingTests : IDisposable
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "eryph-id-client-ct-" + Guid.NewGuid().ToString("N"));
    private readonly IFileSystem _fileSystem = new FileSystem();
    private readonly IdentityChangeTrackingConfig _config;

    public ClientApplicationChangeTrackingTests()
    {
        _config = new IdentityChangeTrackingConfig
        {
            SeedDatabase = true,
            ClientsConfigPath = Path.Combine(_dir, "clients"),
        };
    }

    [Fact]
    public async Task Client_is_exported_then_rebuilt_after_a_database_drop()
    {
        var descriptor = new ClientApplicationDescriptor
        {
            ClientId = "client-1",
            TenantId = TenantId,
            DisplayName = "Client One",
            Certificate = "cert-base64",
            ClientSecret = "hashed-secret",
        };
        descriptor.Scopes.Add("compute:write");

        // Export: the handler reads the client and writes its config file.
        var exportService = new Mock<IClientService>();
        exportService
            .Setup(s => s.Get("client-1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(descriptor);

        var handler = new ClientApplicationChangeHandler(_config, _fileSystem, exportService.Object);
        await handler.HandleChangeAsync(new ClientApplicationChange("client-1", TenantId));

        var path = Path.Combine(_config.ClientsConfigPath, "client-1.json");
        _fileSystem.File.Exists(path).Should().BeTrue("the client must be mirrored to a config file");
        var model = JsonSerializer.Deserialize<ClientConfigModel>(await _fileSystem.File.ReadAllTextAsync(path));
        model!.ClientId.Should().Be("client-1");
        model.X509CertificateBase64.Should().Be("cert-base64");
        model.SharedSecret.Should().Be("hashed-secret");
        model.AllowedScopes.Should().Contain("compute:write");

        // Reseed: a fresh (dropped) database has no client; the seeder re-adds it from the file.
        ClientApplicationDescriptor? added = null;
        var seedService = new Mock<IClientService>();
        seedService
            .Setup(s => s.Get("client-1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientApplicationDescriptor?)null);
        seedService
            .Setup(s => s.Add(It.IsAny<ClientApplicationDescriptor>(), true, It.IsAny<CancellationToken>()))
            .Callback<ClientApplicationDescriptor, bool, CancellationToken>((d, _, _) => added = d)
            .ReturnsAsync((ClientApplicationDescriptor d, bool _, CancellationToken _) => d);

        var seeder = new ClientSeeder(_config, _fileSystem, seedService.Object);
        await seeder.Execute(CancellationToken.None);

        added.Should().NotBeNull("the client must be rebuilt from the file mirror");
        added!.ClientId.Should().Be("client-1");
        added.Certificate.Should().Be("cert-base64");
        added.ClientSecret.Should().Be("hashed-secret", "the stored hashed secret is re-added as hashed");
        added.Scopes.Should().Contain("compute:write");
    }

    [Fact]
    public async Task Export_removes_the_mirror_file_when_the_client_is_gone()
    {
        var path = Path.Combine(_config.ClientsConfigPath, "client-2.json");
        _fileSystem.Directory.CreateDirectory(_config.ClientsConfigPath);
        await _fileSystem.File.WriteAllTextAsync(path, "{\"ClientId\":\"client-2\"}");

        var service = new Mock<IClientService>();
        service
            .Setup(s => s.Get("client-2", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientApplicationDescriptor?)null);

        var handler = new ClientApplicationChangeHandler(_config, _fileSystem, service.Object);
        await handler.HandleChangeAsync(new ClientApplicationChange("client-2", TenantId));

        _fileSystem.File.Exists(path).Should().BeFalse("a removed client must remove its mirror file");
    }

    public void Dispose()
    {
        if (_fileSystem.Directory.Exists(_dir))
            _fileSystem.Directory.Delete(_dir, recursive: true);
    }
}
