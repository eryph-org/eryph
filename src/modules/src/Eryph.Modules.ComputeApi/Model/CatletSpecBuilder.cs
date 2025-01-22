using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.StateDb.Model;

namespace Eryph.Modules.ComputeApi.Model;

public class CatletSpecBuilder(
    IUserRightsProvider userRightsProvider)
    : ResourceSpecBuilder<Catlet>(userRightsProvider)
{
    protected override void CustomizeQuery(ISpecificationBuilder<Catlet> query)
    {
        query.Include(x => x.Drives).ThenInclude(d => d.AttachedDisk);
        query.Include(x => x.ReportedNetworks);
    }
}
