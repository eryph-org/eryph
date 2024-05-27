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

        // Store all DateTimeOffset values as strings in the database.
        // This might be suboptimal for performance, but it is the only
        // way to ensure that filtering, sorting and concurrency tokens
        // work as expected.
        // DateTimeOffsetToBinaryConverter has known issues with filtering
        // and sorting.
        // Converting to DateTime will be break concurrency tokens as the
        // conversion is lossy (the timezone information is lost).
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToDateTimeConverter, DateTimeOffsetUtcComparer>();
    }
}
