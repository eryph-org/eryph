using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Eryph.IdentityDb;

/// <summary>
/// Converts <see cref="DateTimeOffset"/> to <see cref="DateTime"/> for SQLite, which has no native
/// <see cref="DateTimeOffset"/> support. The conversion normalizes to UTC; the paired
/// <see cref="DateTimeOffsetUtcComparer"/> ignores the (now-zero) offset so the value still sorts and
/// works as a concurrency token. Mirrors the converter used by the SQLite state store.
/// </summary>
internal class DateTimeOffsetToDateTimeConverter(ConverterMappingHints? mappingHints)
    : ValueConverter<DateTimeOffset, DateTime>(
        t => t.UtcDateTime,
        t => new DateTimeOffset(t.Ticks, TimeSpan.Zero),
        mappingHints)
{
    public DateTimeOffsetToDateTimeConverter() : this(null) { }
}
