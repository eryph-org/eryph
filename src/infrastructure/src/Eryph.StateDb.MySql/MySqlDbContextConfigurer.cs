using Microsoft.EntityFrameworkCore;

namespace Eryph.StateDb.MySql
{
    public class MySqlDbContextConfigurer(string connectionString)
        : IStateStoreContextConfigurer
    {
        public void Configure(DbContextOptionsBuilder options)
        {
            options.UseMySql(connectionString, new MariaDbServerVersion("10.3.8"));
        }
    }
}