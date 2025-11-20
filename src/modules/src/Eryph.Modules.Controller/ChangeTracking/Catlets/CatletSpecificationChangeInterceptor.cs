using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.ChangeTracking.Catlets;

internal class CatletSpecificationChangeInterceptor(
    IChangeTrackingQueue<CatletSpecificationChange> queue,
    ILogger logger)
    : ChangeInterceptorBase<CatletSpecificationChange>(queue, logger)
{
    protected override Task<Seq<CatletSpecificationChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ChangeTracker.Entries<CatletSpecification>()
            .ToSeq().Strict()
            .Map(e => e.Entity.Id)
            .Distinct()
            .Map(id => new CatletSpecificationChange(id))
            .ToSeq()
            .AsTask();
    }
}
