using Haipa.IdentityDb;
using Microsoft.EntityFrameworkCore;

namespace Haipa.IdentityDb.Sqlite
{
    public class SqliteDbContextConfigurer<TContext> : IDbContextConfigurer<TContext> where TContext : DbContext
    {
        public void Configure(DbContextOptionsBuilder options)
        {
            options.UseSqlite();
        }
    }
}