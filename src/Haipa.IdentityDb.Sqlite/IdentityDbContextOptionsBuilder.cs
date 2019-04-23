using System;
using Microsoft.EntityFrameworkCore;

namespace Haipa.IdentityDb.Sqlite
{
    public static class IdentityDbContextOptionsBuilder
    {

        public static DbContextOptionsBuilder UseSqlite(this DbContextOptionsBuilder optionsBuilder)
        {
            //Most likely haipa-zero will never have multi tenant support 
            //but to keep it consistent we already add the tenant name
            var connectionString = $"Data Source=identity-default.db";

            return optionsBuilder.UseSqlite(connectionString,
                options =>
                {                 
                    options.MigrationsAssembly(typeof(IdentityDbContextOptionsBuilder).Assembly.GetName().Name);
                });
        }

    }
}
