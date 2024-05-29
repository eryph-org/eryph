using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Eryph.StateDb.Sqlite;

internal class DateTimeOffsetToDateTimeConverter(ConverterMappingHints? mappingHints)
    : ValueConverter<DateTimeOffset, DateTime>(
        t => t.UtcDateTime,
        t => new DateTimeOffset(t.Ticks, TimeSpan.Zero),
        mappingHints)
{
    public DateTimeOffsetToDateTimeConverter() : this(null) { }
}
