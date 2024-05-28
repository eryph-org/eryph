using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Eryph.StateDb.Sqlite;

public class SqliteStateStoreContext(DbContextOptions<SqliteStateStoreContext> options)
    : StateStoreContext(options)
{
    protected override void ConfigureConventions(
        ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Convert all DateTimeOffset values to DateTime values.
        // Sqlite does not have native support for DateTimeOffset.
        // The conversion is lossy as we convert to UTC but the
        // custom comparer ignores the time zone information.
        // This way, we can both sort by the datetime value
        // and use it as a concurrency token.
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToDateTimeConverter, DateTimeOffsetUtcComparer>();
    }
}
