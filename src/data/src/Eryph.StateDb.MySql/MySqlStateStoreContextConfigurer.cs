using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace Eryph.StateDb.MySql;

public class MySqlStateStoreContextConfigurer(string connectionString)
    : IStateStoreContextConfigurer
{
    public void Configure(DbContextOptionsBuilder options)
    {
        options.UseMySql(connectionString, ServerVersion.Create(10, 11, 0, ServerType.MariaDb));
    }
}
