using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Eryph.StateDb.Sqlite;

internal class DateTimeOffsetUtcComparer() : ValueComparer<DateTimeOffset>(
    (a, b) => a.Ticks == b.Ticks,
    a => a.Ticks.GetHashCode());
