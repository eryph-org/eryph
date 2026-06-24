using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Eryph.StateDb.Sqlite;

internal class DateTimeOffsetToDateTimeConverter(ConverterMappingHints? mappingHints)
    : ValueConverter<DateTimeOffset, DateTime>(
        t => t.UtcDateTime,
        t => new DateTimeOffset(t.Ticks, TimeSpan.Zero),
        mappingHints)
{
    public DateTimeOffsetToDateTimeConverter() : this(null)
    {
    }
}
