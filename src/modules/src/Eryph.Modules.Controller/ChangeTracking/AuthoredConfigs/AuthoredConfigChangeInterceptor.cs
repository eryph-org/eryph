using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.ChangeTracking.AuthoredConfigs;

internal class AuthoredConfigChangeInterceptor(
    IChangeTrackingQueue<AuthoredConfigChange> queue,
    ILogger logger)
    : ChangeInterceptorBase<AuthoredConfigChange>(queue, logger)
{
    protected override Task<Seq<AuthoredConfigChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            dbContext.ChangeTracker.Entries<AuthoredConfig>().Any()
                ? Seq1(new AuthoredConfigChange())
                : Empty);
}
