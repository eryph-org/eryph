using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.Sqlite;
using Eryph.StateDb.SqlServer;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Eryph.StateDb.Design.Factories;

[UsedImplicitly]
public class SqlServerStateStoreContextDesignTimeFactory
    : IDesignTimeDbContextFactory<SqlServerStateStoreContext>
{
    public SqlServerStateStoreContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SqlServerStateStoreContext>();
        optionsBuilder.UseSqlServer();

        return new SqlServerStateStoreContext(optionsBuilder.Options);
    }
}
