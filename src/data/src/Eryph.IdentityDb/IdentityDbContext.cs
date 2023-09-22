using Eryph.IdentityDb.Entities;
using Microsoft.EntityFrameworkCore;
using YamlDotNet.Core.Tokens;

namespace Eryph.IdentityDb;

public class IdentityDbContext: DbContext
{
    public DbSet<ApplicationEntity> Applications { get; set; }

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

        base.OnModelCreating(modelBuilder);
    }
}