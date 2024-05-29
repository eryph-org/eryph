using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Eryph.StateDb.Sqlite;

internal class DateTimeOffsetUtcComparer() : ValueComparer<DateTimeOffset>(
    (a, b) => a.Ticks == b.Ticks,
    a => a.Ticks.GetHashCode());
