using System;
using Microsoft.EntityFrameworkCore;

namespace Eryph.StateDb.MySql
{
    public static class DbStateDbContextOptionsBuilder
    {
        public static DbContextOptionsBuilder UseMySql(this DbContextOptionsBuilder optionsBuilder)
        {
            var mySqlConnectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTIONSTRING");
            if (string.IsNullOrWhiteSpace(mySqlConnectionString))
                throw new ApplicationException(
                    "missing MySQL connection string (set environment variable MYSQL_CONNECTIONSTRING");


            return optionsBuilder.UseMySql(mySqlConnectionString, // replace with your Connection String
                new MariaDbServerVersion("10.3.8"),
                mysqlOptions =>
                {
                    mysqlOptions.MigrationsAssembly(typeof(DbStateDbContextOptionsBuilder).Assembly.GetName().Name);
                });
        }
    }
}