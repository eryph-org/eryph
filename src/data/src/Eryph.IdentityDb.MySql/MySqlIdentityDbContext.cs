using Microsoft.EntityFrameworkCore;

namespace Eryph.IdentityDb.MySql;

/// <summary>
/// The standalone identity host's MariaDB store context. The model is provider-agnostic, so this derived
/// context only exists to carry the MariaDB migrations in its own assembly. Mirrors
/// <c>MySqlStateStoreContext</c>.
/// </summary>
public class MySqlIdentityDbContext(DbContextOptions<MySqlIdentityDbContext> options)
    : IdentityDbContext(options);
