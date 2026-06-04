using System;
using Microsoft.EntityFrameworkCore;

namespace Eryph.IdentityDb.Sqlite;

/// <summary>
/// The eryph-zero identity store context. SQLite has no native <see cref="DateTimeOffset"/>, so this
/// derived context converts all <see cref="DateTimeOffset"/> values (e.g.
/// <c>RedeemedEnrollmentToken.ExpiresAt</c>) to UTC <see cref="DateTime"/> — keeping the converter on the
/// SQLite context means the shared model stays provider-agnostic. Mirrors <c>SqliteStateStoreContext</c>.
/// </summary>
public class SqliteIdentityDbContext(DbContextOptions<SqliteIdentityDbContext> options)
    : IdentityDbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToDateTimeConverter, DateTimeOffsetUtcComparer>();
    }
}
