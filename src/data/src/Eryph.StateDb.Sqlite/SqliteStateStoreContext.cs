using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Eryph.StateDb.Sqlite;

public class SqliteStateStoreContext(DbContextOptions<SqliteStateStoreContext> options)
    : StateStoreContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OperationLogEntry>().Property(e => e.Timestamp).HasConversion(
            dateTimeOffset => dateTimeOffset.UtcDateTime,
            dateTime => new DateTimeOffset(dateTime));
    }

    protected override void ConfigureConventions(
        ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToDateTimeConverter>();
    }

    private sealed class DateTimeOffsetToDateTimeConverter(ConverterMappingHints? mappingHints = null)
        : ValueConverter<DateTimeOffset, DateTime>(
            v => ToDateTime(v),
            v => ToDateTimeOffset(v),
            mappingHints)
    {
        private static DateTime ToDateTime(DateTimeOffset dateTimeOffset) => dateTimeOffset.UtcDateTime;

        private static DateTimeOffset ToDateTimeOffset(DateTime dateTime) => new(dateTime.ToUniversalTime(), TimeSpan.Zero);
    }
}
