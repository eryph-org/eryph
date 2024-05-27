using Eryph.StateDb.Sqlite;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Eryph.StateDb.Design.Factories;

[UsedImplicitly]
public class SqliteStateStoreContextDesignTimeFactory
    : IDesignTimeDbContextFactory<SqliteStateStoreContext>
{
    public SqliteStateStoreContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SqliteStateStoreContext>();
        optionsBuilder.UseSqlite();

        return new SqliteStateStoreContext(optionsBuilder.Options);
    }
}
