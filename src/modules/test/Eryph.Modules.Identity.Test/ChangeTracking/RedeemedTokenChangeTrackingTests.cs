using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Eryph.IdentityDb;
using Eryph.IdentityDb.Entities;
using Eryph.Modules.Identity.ChangeTracking;
using Eryph.Modules.Identity.ChangeTracking.RedeemedTokens;
using Eryph.Modules.Identity.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Eryph.Modules.Identity.Test.ChangeTracking;

/// <summary>
/// Proves the identity change-tracking round trip for the redeemed enrollment-token domain: a redeemed
/// token is exported to the on-disk mirror and rebuilt from it after the database is dropped — the
/// property that lets eryph-zero treat its identity store as disposable without reopening single-use
/// tokens to restart-replay.
/// </summary>
public class RedeemedTokenChangeTrackingTests : IDisposable
{
    private readonly IdentityChangeTrackingConfig _config;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "eryph-id-ct-" + Guid.NewGuid().ToString("N"));
    private readonly IFileSystem _fileSystem = new FileSystem();

    public RedeemedTokenChangeTrackingTests()
    {
        _config = new IdentityChangeTrackingConfig
        {
            SeedDatabase = true,
            RedeemedTokensConfigPath = Path.Combine(_dir, "redeemed-tokens"),
        };
    }

    public void Dispose()
    {
        if (_fileSystem.Directory.Exists(_dir))
            _fileSystem.Directory.Delete(_dir, true);
    }

    private static IdentityDbContext NewContext(InMemoryDatabaseRoot root)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        options.UseInMemoryDatabase("identity-ct", root);
        IdentityDbModel.ApplyOpenIddict(options);
        return new IdentityDbContext(options.Options);
    }

    [Fact]
    public async Task Redeemed_token_is_exported_then_rebuilt_after_a_database_drop()
    {
        var path = Path.Combine(_config.RedeemedTokensConfigPath, "jti-1.json");

        // Source database: redeem a token, then export it through the change handler.
        var root = new InMemoryDatabaseRoot();
        await using (var context = NewContext(root))
        {
            var repository = new IdentityDbRepository<RedeemedEnrollmentToken>(context);
            await repository.AddAsync(new RedeemedEnrollmentToken
            {
                Jti = "jti-1",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            });
            await repository.SaveChangesAsync();

            var handler = new RedeemedTokenChangeHandler(_config, _fileSystem, repository);
            await handler.HandleChangeAsync(new RedeemedTokenChange("jti-1"));
        }

        _fileSystem.File.Exists(path).Should().BeTrue("the redeemed token must be mirrored to the config file");

        // Fresh, empty database (a dropped + recreated store) — then seed from the file mirror.
        var freshRoot = new InMemoryDatabaseRoot();
        await using (var context = NewContext(freshRoot))
        {
            var repository = new IdentityDbRepository<RedeemedEnrollmentToken>(context);
            (await repository.GetByIdAsync("jti-1")).Should().BeNull("the fresh database starts empty");

            var seeder = new RedeemedTokenSeeder(_config, _fileSystem, repository);
            await seeder.Execute(CancellationToken.None);
        }

        await using (var verifyContext = NewContext(freshRoot))
        {
            var repository = new IdentityDbRepository<RedeemedEnrollmentToken>(verifyContext);
            (await repository.GetByIdAsync("jti-1")).Should()
                .NotBeNull("the redeemed token must survive a database drop via the file mirror");
        }
    }

    [Fact]
    public async Task Export_removes_the_mirror_file_when_the_token_is_gone()
    {
        var path = Path.Combine(_config.RedeemedTokensConfigPath, "jti-2.json");
        _fileSystem.Directory.CreateDirectory(_config.RedeemedTokensConfigPath);
        await _fileSystem.File.WriteAllTextAsync(path, "{\"Jti\":\"jti-2\"}");

        var root = new InMemoryDatabaseRoot();
        await using var context = NewContext(root);
        var repository = new IdentityDbRepository<RedeemedEnrollmentToken>(context);
        var handler = new RedeemedTokenChangeHandler(_config, _fileSystem, repository);

        // No such token in the database -> the export must delete the stale mirror file.
        await handler.HandleChangeAsync(new RedeemedTokenChange("jti-2"));

        _fileSystem.File.Exists(path).Should().BeFalse("a removed token must remove its mirror file");
    }
}
