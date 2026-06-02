using System.Threading;
using System.Threading.Tasks;
using Eryph.IdentityDb.Entities;
using Eryph.ModuleCore.ChangeTracking;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Identity.ChangeTracking.RedeemedTokens;

internal class RedeemedTokenChangeInterceptor : ChangeInterceptorBase<RedeemedTokenChange>
{
    public RedeemedTokenChangeInterceptor(
        IChangeTrackingQueue<RedeemedTokenChange> queue,
        ILogger logger)
        : base(queue, logger)
    {
    }

    protected override Task<Seq<RedeemedTokenChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ChangeTracker.Entries<RedeemedEnrollmentToken>()
            .ToSeq().Strict()
            .Map(e => e.Entity.Jti)
            .Distinct()
            .Map(jti => new RedeemedTokenChange(jti))
            .ToSeq().Strict()
            .AsTask();
    }
}
