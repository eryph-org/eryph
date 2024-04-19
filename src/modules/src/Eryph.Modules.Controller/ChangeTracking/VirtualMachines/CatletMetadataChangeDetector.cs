using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Eryph.Modules.Controller.ChangeTracking.VirtualMachines;

internal class CatletMetadataChangeDetector : IChangeDetector<CatletMetadataChange>
{ 
    public Task<Seq<CatletMetadataChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ChangeTracker.Entries<CatletMetadata>().ToList()
            .Map(e => e.Entity.Id)
            .Distinct()
            .Map(metadataId => new CatletMetadataChange(metadataId))
            .ToSeq()
            .AsTask();
    }
}
