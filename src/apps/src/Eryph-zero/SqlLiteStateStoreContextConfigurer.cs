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
        options.UseSqlite("Data Source=state.db");
    }
}