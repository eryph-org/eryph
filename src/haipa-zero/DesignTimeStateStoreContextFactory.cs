using Haipa.IdentityDb;
using Haipa.IdentityDb.Sqlite;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Haipa.Controller
{
    [UsedImplicitly]
    public class DesignTimeIdentityContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
    {
        public IdentityDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
            optionsBuilder.UseSqlite();
            optionsBuilder.UseOpenIddict();

            return new IdentityDbContext(optionsBuilder.Options);
        }
    }
    
}