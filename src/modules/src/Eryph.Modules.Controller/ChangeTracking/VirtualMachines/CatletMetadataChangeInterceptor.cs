using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.ChangeTracking.VirtualMachines;

internal class CatletMetadataChangeInterceptor : ChangeInterceptorBase<CatletMetadataChange>
{
    public CatletMetadataChangeInterceptor(
        IChangeTrackingQueue<CatletMetadataChange> queue)
        : base(queue)
    {
    }

    protected override Task<Option<CatletMetadataChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ChangeTracker.Entries<CatletMetadata>().ToList()
            .Map(e => e.Entity.Id)
            .Distinct()
            .Match(
                Empty: () => Task.FromResult(Option<CatletMetadataChange>.None),
                More: p => Task.FromResult(Some(new CatletMetadataChange
                {
                    Ids = p.ToList(),
                })));
    }
}
