using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.ChangeTracking.Catlets;

internal class CatletSpecificationVersionChangeInterceptor(
    IChangeTrackingQueue<CatletSpecificationVersionChange> queue,
    ILogger logger)
    : ChangeInterceptorBase<CatletSpecificationVersionChange>(queue, logger)
{
    protected override Task<Seq<CatletSpecificationVersionChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ChangeTracker.Entries<CatletSpecificationVersion>().ToList()
            .Map(e => (e.Entity.Id, e.Entity.SpecificationId))
            .Distinct()
            .Map(c => new CatletSpecificationVersionChange(c.Id, c.SpecificationId))
            .ToSeq()
            .AsTask();
    }
}
