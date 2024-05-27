using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Eryph.StateDb.Sqlite;

public class SqliteStateStoreContextConfigurer(string connectionString)
    : IStateStoreContextConfigurer
{
    public virtual void Configure(DbContextOptionsBuilder options)
    {
        options.UseSqlite(connectionString);
        options.ConfigureWarnings(x => x.Ignore(RelationalEventId.AmbientTransactionWarning));
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
}
