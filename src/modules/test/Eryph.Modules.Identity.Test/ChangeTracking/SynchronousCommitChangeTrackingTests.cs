using System;
using System.Linq;
using System.Threading.Tasks;
using Eryph.IdentityDb;
using Eryph.IdentityDb.Entities;
using Eryph.ModuleCore.ChangeTracking;
using Eryph.Modules.Identity.ChangeTracking.RedeemedTokens;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Eryph.Modules.Identity.Test.ChangeTracking;

/// <summary>
/// Regression guard for the change-tracking interceptor on a <em>relational</em> identity store.
/// OpenIddict's EF Core application store commits its delete transaction synchronously
/// (<c>RelationalTransaction.Commit</c>), so EF Core invokes the interceptor's <em>synchronous</em>
/// callbacks rather than the async ones. Those used to throw <see cref="NotSupportedException"/>,
/// which turned every client delete on the persistent identity DB (MariaDB/SQLite) into a 500 — the
/// in-memory provider never exercised the path because it has no relational transactions.
/// </summary>
public class SynchronousCommitChangeTrackingTests
{
    [Fact]
    public async Task Synchronously_committed_delete_is_tracked_and_enqueued()
    {
        // A single open connection keeps the in-memory SQLite schema alive across both contexts.
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        // The queue is a singleton in production; the interceptor is scoped (one per DbContext).
        var queue = new ChangeTrackingQueue<RedeemedTokenChange>();

        DbContextOptions<IdentityDbContext> NewOptions()
        {
            var builder = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(new RedeemedTokenChangeInterceptor(queue, NullLogger.Instance));
            IdentityDbModel.ApplyOpenIddict(builder);
            return builder.Options;
        }

        await using (var context = new IdentityDbContext(NewOptions()))
        {
            await context.Database.EnsureCreatedAsync();
            context.RedeemedEnrollmentTokens.Add(new RedeemedEnrollmentToken
            {
                Jti = "jti-sync",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            });
            await context.SaveChangesAsync();
        }

        // Start capturing only now, so the assertion sees the delete and not the seed insert.
        queue.Enable();

        await using (var context = new IdentityDbContext(NewOptions()))
        {
            var entity = await context.RedeemedEnrollmentTokens.SingleAsync(t => t.Jti == "jti-sync");
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.RedeemedEnrollmentTokens.Remove(entity);
            await context.SaveChangesAsync();

            // The synchronous commit is the exact call OpenIddict makes on its delete path; before the
            // fix this threw NotSupportedException from the interceptor and rolled the delete back.
            var commit = () => transaction.Commit();
            commit.Should().NotThrow();
        }

        queue.GetCount().Should().Be(1, "the synchronously committed deletion must still be enqueued for export");
        var item = await queue.DequeueAsync();
        item.Changes.Jti.Should().Be("jti-sync");
    }
}
