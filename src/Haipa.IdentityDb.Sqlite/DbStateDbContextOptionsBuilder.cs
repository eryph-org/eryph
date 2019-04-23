using System;
using Microsoft.EntityFrameworkCore;

namespace Haipa.IdentityDb.Sqlite
{
    public static class IdentityDbContextOptionsBuilder
    {

        public static DbContextOptionsBuilder UseSqlite(this DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = $"Data Source=identity.db";

            return optionsBuilder.UseSqlite(connectionString, // replace with your Connection String
                options =>
                {
                    
                    options.MigrationsAssembly(typeof(IdentityDbContextOptionsBuilder).Assembly.GetName().Name);
                });
        }

    }
}
