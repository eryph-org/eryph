using Eryph.IdentityDb.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eryph.IdentityDb;

// Shared identity model. Each packaging derives a provider-specific context (Sqlite/MySql/InMemory)
// and registers it via that provider's RegisterXxxIdentityStore extension — mirroring StateStoreContext.
// The non-generic DbContextOptions constructor lets the derived contexts pass their own
// DbContextOptions<TDerived> through to the base, so a single model serves every provider.
public class IdentityDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<ApplicationEntity> Applications { get; set; }

    public DbSet<RedeemedEnrollmentToken> RedeemedEnrollmentTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationEntity>()
            .HasDiscriminator<IdentityApplicationType>("AppType")
            .HasValue<ApplicationEntity>(IdentityApplicationType.OAuth)
            .HasValue<ClientApplicationEntity>(IdentityApplicationType.Client);

        modelBuilder.Entity<RedeemedEnrollmentToken>(entity =>
        {
            entity.HasKey(t => t.Jti);
            entity.Property(t => t.Jti).HasMaxLength(64);
        });

        // OpenIddict indexes (ApplicationId, Status, Subject, Type) on both tokens and authorizations.
        // On MariaDB/MySQL with utf8mb4 (4 bytes/char) the default Subject length (400) pushes the
        // tokens index past the 3072-byte key limit. Subject holds a principal identifier, for which
        // 256 is ample; capping it keeps the composite index within the limit. Harmless for SQLite
        // (eryph-zero) and the in-memory provider (tests), which do not enforce column lengths.
        modelBuilder.Entity<TokenEntity>().Property(t => t.Subject).HasMaxLength(256);
        modelBuilder.Entity<AuthorizationEntity>().Property(a => a.Subject).HasMaxLength(256);

        base.OnModelCreating(modelBuilder);
    }
}
