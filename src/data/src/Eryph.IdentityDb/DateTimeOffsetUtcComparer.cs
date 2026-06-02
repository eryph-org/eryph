using System;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Eryph.IdentityDb;

/// <summary>
/// Compares <see cref="DateTimeOffset"/> values by their UTC ticks only, ignoring the offset. Paired with
/// <see cref="DateTimeOffsetToDateTimeConverter"/> so the lossy SQLite storage still compares correctly.
/// </summary>
internal class DateTimeOffsetUtcComparer() : ValueComparer<DateTimeOffset>(
    (a, b) => a.UtcTicks == b.UtcTicks,
    a => a.UtcTicks.GetHashCode());
