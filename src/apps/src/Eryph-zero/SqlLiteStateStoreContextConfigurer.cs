using System.IO;
using Eryph.Runtime.Zero.Configuration;
using Eryph.StateDb;
using Microsoft.EntityFrameworkCore;

namespace Eryph.Runtime.Zero;

public class SqlLiteStateStoreContextConfigurer : IDbContextConfigurer<StateStoreContext>
{

    public SqlLiteStateStoreContextConfigurer()
    {
    }

    public void Configure(DbContextOptionsBuilder options)
    {
        var path = Path.Combine(ZeroConfig.GetPrivateConfigPath(), "state.db");
        options.UseSqlite($"Data Source=\"{path}\"");
    }
}