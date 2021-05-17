using Microsoft.EntityFrameworkCore;

namespace Haipa.StateDb.MySql
{
    public class MySqlDbContextConfigurer<TContext> : IDbContextConfigurer<TContext> where TContext : DbContext
    {
        public void Configure(DbContextOptionsBuilder options)
        {
            options.UseMySql();
        }
    }
}