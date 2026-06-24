using Microsoft.EntityFrameworkCore;

namespace Eryph.StateDb.MySql;

public class MySqlStateStoreContext(DbContextOptions<MySqlStateStoreContext> options)
    : StateStoreContext(options);
