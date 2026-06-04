using System.Threading;
using System.Threading.Tasks;
using Eryph.IdentityDb.Entities;
using Eryph.ModuleCore.ChangeTracking;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Identity.ChangeTracking.Clients;

internal class ClientApplicationChangeInterceptor : ChangeInterceptorBase<ClientApplicationChange>
{
    public ClientApplicationChangeInterceptor(
        IChangeTrackingQueue<ClientApplicationChange> queue,
        ILogger logger)
        : base(queue, logger)
    {
    }

    protected override Task<Seq<ClientApplicationChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ChangeTracker.Entries<ClientApplicationEntity>()
            .ToSeq().Strict()
            .Where(e => !string.IsNullOrEmpty(e.Entity.ClientId))
            .Map(e => new ClientApplicationChange(e.Entity.ClientId!, e.Entity.TenantId))
            .Distinct()
            .ToSeq().Strict()
            .AsTask();
    }
}
