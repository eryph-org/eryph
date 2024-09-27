using Ardalis.Specification;
using Eryph.Modules.AspNetCore;

namespace Eryph.Modules.ComputeApi.Model;

public class VirtualNetworkSpecBuilder(
    IUserRightsProvider userRightsProvider)
    : ResourceSpecBuilder<StateDb.Model.VirtualNetwork>(userRightsProvider)
{
    protected override void CustomizeQuery(
        ISpecificationBuilder<StateDb.Model.VirtualNetwork> query)
    {
        query.Include(x => x.Project);
    }
}
