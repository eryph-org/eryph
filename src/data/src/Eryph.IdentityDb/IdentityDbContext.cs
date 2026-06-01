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

        base.OnModelCreating(modelBuilder);
    }
}