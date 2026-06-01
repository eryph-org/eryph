using Eryph.IdentityDb.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eryph.IdentityDb;

public class IdentityDbContext: DbContext
{
    public DbSet<ApplicationEntity> Applications { get; set; }

    public DbSet<RedeemedEnrollmentToken> RedeemedEnrollmentTokens { get; set; }

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

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
        // 256 is ample; capping it keeps the composite index within the limit. Harmless for the
        // in-memory provider (eryph-zero, tests), which ignores column lengths.
        modelBuilder.Entity<TokenEntity>().Property(t => t.Subject).HasMaxLength(256);
        modelBuilder.Entity<AuthorizationEntity>().Property(a => a.Subject).HasMaxLength(256);

        base.OnModelCreating(modelBuilder);
    }
}