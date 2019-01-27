using System;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace Haipa.StateDb.MySql
{
    public static class DbStateDbContextOptionsBuilder
    {

        public static DbContextOptionsBuilder UseMySql(this DbContextOptionsBuilder optionsBuilder)
        {
            var mySqlConnectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTIONSTRING");
            if (string.IsNullOrWhiteSpace(mySqlConnectionString))
                throw new ApplicationException("missing MySQL connection string (set environment variable MYSQL_CONNECTIONSTRING");


            return optionsBuilder.UseMySql(mySqlConnectionString, // replace with your Connection String
                mysqlOptions =>
                {
                    mysqlOptions.MigrationsAssembly(typeof(DbStateDbContextOptionsBuilder).Assembly.GetName().Name);                    
                    mysqlOptions.ServerVersion(new Version(10, 3, 8),
                        ServerType.MariaDb); // replace with your Server Version and Type
                });
        }

    }
}
